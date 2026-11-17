using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU（3対3 の陣取り、一人称）。第1段階：マップと6つの武器を、わら人形で試せる「練習場」。
    /// 操作：WASD 移動・Shift 走る・Space ジャンプ・マウス 見回す・左クリック 攻撃・右クリック 飛び道具／投げる・E 移動わざ・1〜6／ホイール 武器を変える・Esc 一時停止。
    /// 島とは別のシーン（Kassen が作る）で遊ぶ。
    /// </summary>
    public partial class Sengoku : MonoBehaviour
    {
        public static Sengoku I;

        Camera _cam;
        GameObject _rig;
        CharacterController _cc;
        float _yaw = 90f, _pitch = 5f;
        Vector3 _vel;
        bool _grounded;
        float _hp = 100, _fov = 75f;
        bool _paused;
        int _kills;
        AudioSource _audio;
        const float Gravity = 20f, Eye = 1.6f;

        /// <summary>match＝3対3 の試合（false なら練習場）。level：0 やさしい・1 ふつう・2 つよい。</summary>
        public static void Begin(Scene scene, bool match = false, int level = 1, int theme = -1)
        {
            var go = new GameObject("SENGOKU");
            if (scene.IsValid()) SceneManager.MoveGameObjectToScene(go, scene);
            var s = go.AddComponent<Sengoku>();
            s._match = match; s._level = Mathf.Clamp(level, 0, 2);
            s._theme = theme < 0 ? Random.Range(0, StageNamesSg.Length) : Mathf.Clamp(theme, 0, StageNamesSg.Length - 1); // 舞台は試合ごとにランダム
            ThemeNow = s._theme;
            s.Init();
        }

        void Init()
        {
            I = this;
            _cam = Camera.main;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 0;
            Sounds();
            if (!_match) { BuildMap(); PlaceDummies(); }
            _rig = new GameObject("自分（SENGOKU）");
            _rig.transform.SetParent(transform, false);
            _rig.transform.position = WestCastle + new Vector3(14f, 0.2f, 0);
            _cc = _rig.AddComponent<CharacterController>();
            _cc.height = 1.75f; _cc.radius = 0.35f; _cc.center = new Vector3(0, 0.9f, 0); _cc.stepOffset = 0.45f; _cc.slopeLimit = 50f;
            BuildWeapons();
            BuildHud();
            Select(0);
            if (_match) InitMatch();
            if (_cam != null) _cam.fieldOfView = _fov;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            foreach (var b in new[] { _hud, _pauseB, _resultB, _selB, _intel }) if (b != null) Destroy(b.gameObject);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            foreach (var u in _units) if (u.tag != null) Destroy(u.tag.gameObject);
            if (_viewRoot != null) Destroy(_viewRoot.gameObject);
            Time.timeScale = 1f;
        }

        /// <summary>確認用（--tour の sengoku:武器:場面）。view・overview・wire・ride・jet・tower・aim。</summary>
        public static void TestPose(int weapon, string pose)
        {
            var s = I;
            if (s == null) return;
            s.Select(weapon);
            switch (pose)
            {
                case "overview":
                    s._camOverride = true; s._camPos = new Vector3(-55, 55, -75); s._camLook = new Vector3(0, 0, 0);
                    if (s._viewRoot) s._viewRoot.gameObject.SetActive(false);
                    break;
                case "wire":
                    s.Teleport(new Vector3(-9, 7, 5)); s._yaw = 60; s._pitch = -12;
                    s._wire = true; s._anchor = new Vector3(0, 17.5f, 2.5f); s._frozen = true; s.ShowLine(s._anchor, new Color(0.4f, 1f, 1f), 99);
                    break;
                case "ride":
                    s.Teleport(new Vector3(-24, -0.2f, 2)); s._yaw = 90; s._pitch = 8; s._ride = true;
                    break;
                case "jet":
                    s.Teleport(new Vector3(-30, 9, 0)); s._yaw = 90; s._pitch = 10; s._flameT = 99; s._frozen = true;
                    break;
                case "tower":
                    s.Teleport(new Vector3(0, 8.7f, 38)); s._yaw = 180; s._pitch = 12;
                    break;
                case "aim":
                    s.Teleport(new Vector3(-48, 0.2f, 0)); s._yaw = 90; s._pitch = 2; s._zoom = true; s._charge = 0.8f;
                    break;
                default:
                    s._yaw = 90; s._pitch = 4;
                    break;
            }
        }

        void Leave()
        {
            Kassen.EndMatch();
            Destroy(gameObject);
        }

        // ───────── 毎フレーム ─────────

        void Update()
        {
            float dt = Time.deltaTime;
            bool result = _match && (_phase == MPhase.Result || _phase == MPhase.Select);
            bool play = !_paused && !Ts.ShotMode && !result && (!_match || (!_dead && _phase == MPhase.Fight));
            bool lockIt = !_paused && !Ts.ShotMode && !result;
            Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockIt;
            if (_paused) { if (Act.Back.Down) Resume(); HudTick(); return; }
            if (!Ts.ShotMode && !result && Act.Back.Down) { Pause(); return; }
            if (_match) { MatchTick(dt); UnitsTick(dt); }
            if (_match) IntelTick(Time.unscaledDeltaTime);
            if (_me != null) { _me.frontGuard = _w == 2 && _ride ? 0.7f : 0f; _me.facing = Forward; }

            if (play)
            {
                var m = Act.LookMouse + Act.LookStick;
                _yaw += m.x * 2.2f;
                _pitch = Mathf.Clamp(_pitch - m.y * 2.2f, -85f, 85f);
                if (!_match)
                {
                    // 練習場では、いつでも持ちかえられる（試合はラウンドの始めにえらぶ）
                    for (int i = 0; i < Weapons.Length; i++) if (Act.Number(i)) Select(i);
                    float sc = Act.Scroll;
                    if (sc > 0.5f) Select((_w + Weapons.Length - 1) % Weapons.Length);
                    if (sc < -0.5f) Select((_w + 1) % Weapons.Length);
                }
                WeaponInput(dt);
            }
            if (!_frozen) Move(play, dt);
            Shots(dt);
            WeaponTick(dt);
            CameraTick(dt);
            HudTick();
        }

        bool _frozen;

        Vector3 Forward => Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
        Vector3 Right => Quaternion.Euler(0, _yaw, 0) * Vector3.right;
        public Vector3 EyePos => _rig.transform.position + Vector3.up * Eye;
        public Vector3 Aim => Quaternion.Euler(_pitch, _yaw, 0) * Vector3.forward;

        void Move(bool play, float dt)
        {
            Vector2 mv = play ? Act.Move.ReadValue<Vector2>() : Vector2.zero;
            bool run = play && Act.Run.Held;
            var wish = (Forward * mv.y + Right * mv.x) * (run ? 9.5f : 6.5f) * SpeedMul;

            if (!AbilityMove(play, dt))
            {
                var flat = new Vector3(_vel.x, 0, _vel.z);
                if (_grounded)
                {
                    flat = Vector3.Lerp(flat, wish, 1 - Mathf.Exp(-14f * dt));
                    if (play && Act.Jump.Down) { _vel.y = 7.5f; _grounded = false; Play(_sJump, 0.4f); }
                }
                else flat = Vector3.MoveTowards(flat, wish, 9f * dt) + (flat.magnitude > wish.magnitude + 0.5f ? Vector3.zero : Vector3.zero);
                _vel.x = flat.x; _vel.z = flat.z;
                _vel.y -= Gravity * dt;
            }
            var flags = _cc.Move(_vel * dt);
            _grounded = _cc.isGrounded;
            if (_grounded && _vel.y < 0) _vel.y = -2f;
            if ((flags & CollisionFlags.Above) != 0 && _vel.y > 0) _vel.y = 0;
            // 落ちたら天守閣の前へもどす
            if (_rig.transform.position.y < -19f) Teleport(_match ? SpawnOf(1, 0) : WestCastle + new Vector3(14f, 0.5f, 0));
        }

        public void Teleport(Vector3 p)
        {
            _cc.enabled = false; _rig.transform.position = p; _cc.enabled = true; _vel = Vector3.zero;
        }

        bool _camOverride; Vector3 _camPos, _camLook;

        void CameraTick(float dt)
        {
            if (_cam == null) return;
            if (SpectateCam()) return;
            if (_viewRoot != null && !_camOverride && !_viewRoot.gameObject.activeSelf && !(_match && _phase == MPhase.Select)) _viewRoot.gameObject.SetActive(true);
            if (_camOverride) { _cam.transform.position = _camPos; _cam.transform.rotation = Quaternion.LookRotation(_camLook - _camPos); _cam.fieldOfView = 60; return; }
            _cam.transform.position = EyePos;
            _cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0) * Quaternion.Euler(_tilt);
            _tilt = Vector3.Lerp(_tilt, Vector3.zero, 1 - Mathf.Exp(-8f * dt));
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _fov + FovAdd, 1 - Mathf.Exp(-10f * dt));
        }

        Vector3 _tilt;
        void Kick(float pitchUp) => _tilt.x -= pitchUp;

        // ───────── 音 ─────────

        static AudioClip _sHit, _sHeavy, _sWhoosh, _sBoom, _sJump, _sClick;

        static void Sounds()
        {
            if (_sHit != null) return;
            _sHit = Synth.Hit(false); _sHeavy = Synth.Hit(true); _sWhoosh = Synth.Whoosh(); _sBoom = Synth.Boom(); _sJump = Synth.Whoosh(); _sClick = Synth.Click();
        }

        void Play(AudioClip c, float vol) { if (c != null && _audio != null) _audio.PlayOneShot(c, vol); }

        // ───────── 表示（板） ─────────

        Board _hud, _pauseB;
        Text _wName, _hint, _kill, _gaugeLabel;
        RectTransform _gauge, _hpFill;
        Image _gaugeImg;
        readonly Text[] _wList = new Text[6];
        readonly List<(Text t, float life, Vector3 world)> _dmg = new List<(Text, float, Vector3)>();
        Text _wTag;

        void BuildHud()
        {
            _hud = Board.Create("SENGOKU の表示", new Vector2(1920, 1080), 40);
            _hud.Apply();
            var r = _hud.root;
            Board.Fans(r, new Rect(16, 16, 1888, 1048), new Color(1, 1, 1, 0.65f), 80);
            // 照準
            Board.Ring(r, new Vector2(960, 540), 16, new Color(1, 1, 1, 0.85f), false);
            var dot = Board.Panel(r, new Rect(957, 537, 6, 6), Board.Teal); dot.sprite = Board.DiscSprite;
            // 上
            Board.Display(r, new Rect(60, 34, 600, 70), "SENGOKU", 56, Board.Teal, TextAnchor.MiddleLeft);
            Board.Label(r, new Rect(64, 100, 600, 30), _match ? "" : "練習場　―　わら人形で武器をためす", 20, Color.white);
            _kill = Board.Label(r, new Rect(1260, 40, 600, 40), "", 26, Board.Gold, TextAnchor.MiddleRight);
            // 体力
            Board.NameTag(r, new Rect(46, 820, 50, 170), "あなた", Board.Teal, false, 28);
            Board.Panel(r, new Rect(118, 950, 420, 20), new Color(0.02f, 0.05f, 0.1f, 0.85f));
            _hpFill = (RectTransform)Board.Panel(r, new Rect(120, 952, 416, 16), Board.Teal).transform;
            Board.Label(r, new Rect(120, 920, 300, 26), "体力", 18, Color.white);
            // 移動わざのゲージ
            Board.Panel(r, new Rect(860, 600, 200, 8), new Color(0.02f, 0.05f, 0.1f, 0.7f));
            _gaugeImg = Board.Panel(r, new Rect(860, 600, 200, 8), Board.Gold);
            _gauge = (RectTransform)_gaugeImg.transform;
            _gaugeLabel = Board.Label(r, new Rect(760, 612, 400, 26), "", 17, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            // 武器
            _wTag = Board.NameTag(r, new Rect(1810, 640, 56, 260), "二刀流", Board.Vermilion, true, 30);
            _wName = Board.Display(r, new Rect(1200, 830, 590, 70), "", 44, Color.white, TextAnchor.MiddleRight);
            for (int i = 0; i < 6; i++)
                _wList[i] = Board.Label(r, new Rect(1440, 640 + i * 30, 350, 28), "", 18, Color.white, TextAnchor.MiddleRight);
            _hint = Board.Label(r, new Rect(980, 900, 810, 140), "", 18, new Color(1, 1, 1, 0.85f), TextAnchor.UpperRight);
            Board.Label(r, new Rect(60, 1010, 900, 30), _match ? "WASD 移動　Shift 走る　Space ジャンプ　F 殴る（大将落としは近接だけ）　Esc 一時停止　自分の城の中で回復" : "WASD 移動　Shift 走る　Space ジャンプ　1〜6・ホイール 武器　Esc 一時停止", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
            for (int i = 0; i < 14; i++)
            {
                var t = Board.Display(r, new Rect(0, 0, 200, 50), "", 34, Board.Gold);
                t.text = "";
                _dmg.Add((t, 0, Vector3.zero));
            }
        }

        /// <summary>数字の代わりに文字を出す（「効かない」など）。</summary>
        public void ShowText(Vector3 at, string text, Color c)
        {
            int k = 0; float best = 99;
            for (int i = 0; i < _dmg.Count; i++) if (_dmg[i].life < best) { best = _dmg[i].life; k = i; }
            var t = _dmg[k].t;
            t.font = Ts.Font; t.text = text; t.color = c;
            _dmg[k] = (t, 0.7f, at + Vector3.up * 0.6f);
        }

        public void ShowDamage(Vector3 at, float dmg, bool down)
        {
            int k = 0; float best = 99;
            for (int i = 0; i < _dmg.Count; i++) if (_dmg[i].life < best) { best = _dmg[i].life; k = i; }
            var t = _dmg[k].t;
            t.font = Board.DisplayFont;
            t.text = down ? $"{dmg:0}!" : $"{dmg:0}";
            t.color = down ? Board.Vermilion : Board.Gold;
            _dmg[k] = (t, 0.9f, at + Vector3.up * 0.4f);
            if (down) { _kills++; Play(_sHeavy, 0.6f); }
        }

        void HudTick()
        {
            if (_hud == null) return;
            var w = Weapons[_w];
            _wName.text = w.en;
            for (int i = 0; i < 6; i++)
            {
                _wList[i].text = _match ? (i == _w ? $"{Owners[i]}の{Weapons[i].name}" : "") : $"{Weapons[i].name}　{i + 1}";
                _wList[i].color = i == _w ? Board.Teal : new Color(1, 1, 1, 0.55f);
                _wList[i].fontSize = i == _w ? 22 : 18;
            }
            _hint.text = w.hint;
            _kill.text = _match ? "" : $"倒したわら人形　{_kills}";
            MatchHudTick();
            _hpFill.sizeDelta = new Vector2(416 * _hp / 100f, 16);
            float g = Gauge01;
            _gauge.sizeDelta = new Vector2(200 * g, 8);
            _gaugeImg.color = g >= 1 ? Board.Teal : Board.Gold;
            _gaugeLabel.text = GaugeText;
            float udt = Time.unscaledDeltaTime, s = Screen.height / 1080f;
            for (int i = 0; i < _dmg.Count; i++)
            {
                var (t, life, world) = _dmg[i];
                if (life <= 0) { if (t.text != "") t.text = ""; continue; }
                life -= udt; world += Vector3.up * udt * 1.2f;
                _dmg[i] = (t, life, world);
                var sp = _cam != null ? _cam.WorldToScreenPoint(world) : Vector3.back;
                if (sp.z <= 0) { t.text = ""; continue; }
                var rt = (RectTransform)t.transform;
                rt.anchoredPosition = new Vector2((sp.x - Screen.width / 2f) / s + 960 - 100, -(540 - (sp.y - Screen.height / 2f) / s) + 25);
                t.canvasRenderer.SetAlpha(Mathf.Clamp01(life * 2f));
            }
        }

        void Pause()
        {
            _paused = true; Time.timeScale = 0f;
            if (_pauseB == null)
            {
                _pauseB = Board.Create("一時停止（SENGOKU）", new Vector2(720, 420), 50);
                _pauseB.worldOffset = new Vector3(0, 0, 1.2f);
                _pauseB.Apply();
                var r = _pauseB.root;
                Board.Frame(r, new Rect(0, 0, 720, 420));
                Board.Display(r, new Rect(0, 40, 720, 90), "PAUSE", 76, Board.Teal);
                Board.Label(r, new Rect(0, 130, 720, 34), _match ? "SENGOKU　3対3" : "SENGOKU 練習場", 24, Color.white, TextAnchor.MiddleCenter);
                Board.ButtonNode(r, new Rect(210, 200, 300, 64), "つづける（Esc）", 26, Resume);
                Board.ButtonNode(r, new Rect(210, 285, 300, 56), _match ? "降参して島へもどる" : "島へもどる", 22, () =>
                {
                    if (_match) { if (!Ts.ShotMode) Kassen.RewardSengoku(false, _level); Hud.Toast("SENGOKU：また挑戦してね"); }
                    else Hud.Toast($"SENGOKU 練習場：わら人形を {_kills} 体倒しました");
                    Leave();
                });
            }
            _pauseB.Alpha = 1;
        }

        void Resume()
        {
            _paused = false; Time.timeScale = 1f;
            if (_pauseB != null) _pauseB.Alpha = 0;
        }
    }
}
