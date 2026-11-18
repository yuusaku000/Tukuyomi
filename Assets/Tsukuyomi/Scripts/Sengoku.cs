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
            s._theme = !match ? 0 : theme < 0 ? Random.Range(0, StageNamesSg.Length) : Mathf.Clamp(theme, 0, StageNamesSg.Length - 1); // 舞台は試合ごとにランダム。練習場はいつも城下町（的と立ち位置が城下町に合わせてある）
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
            _rig.transform.position = PracticeSpawn; // 練習場の立ち位置（前は城の前の大きな箱の中に立っていた）
            _cc = _rig.AddComponent<CharacterController>();
            _cc.height = 1.75f; _cc.radius = 0.35f; _cc.center = new Vector3(0, 0.9f, 0); _cc.stepOffset = 0.45f; _cc.slopeLimit = 50f;
            BuildWeapons();
            BuildHud();
            Select(0);
            if (_match) InitMatch();
            // 画面の縦横の比に合わせて、表示を画面のふちに寄せる（ダメージの数字と目標の印は、画面のまん中が基準のまま）
            if (_hud != null)
            {
                var keep = new List<Component>();
                foreach (var d in _dmg) keep.Add(d.t);
                foreach (var m in _marks) { keep.Add(m.ring); keep.Add(m.label); keep.Add(m.dist); keep.Add(m.arrow); }
                _hud.FitToScreen(keep.ToArray());
            }
            if (_cam != null) _cam.fieldOfView = _fov;
            VrInit();
        }

        void OnDestroy()
        {
            if (I == this) { I = null; Vr.SimSwing = false; }
            VrEnd();
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
                case "vrswing": // 確認用（--vr-sim）：右手を振りつづけて、振る攻撃が出るか見る
                    s.Teleport(new Vector3(-48, 0.2f, 0)); s._yaw = 90; s._testPlay = true; Vr.SimSwing = true;
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
            bool play = !_paused && (!Ts.ShotMode || _testPlay) && !result && (!_match || (!_dead && _phase == MPhase.Fight));
            bool lockIt = !_paused && !Ts.ShotMode && !result;
            Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockIt;
            if (QuitHoldTick()) return;
            if (_paused) { if (Act.Back.Down) Resume(); AimLineTick(false); HudTick(); return; } // VR：止めている間は照準の線と手の武器を隠す
            if (!Ts.ShotMode && !result && Act.Back.Down) { Pause(); return; }
            if (_match) { MatchTick(dt); UnitsTick(dt); }
            if (_match) IntelTick(Time.unscaledDeltaTime);
            if (_me != null) { _me.frontGuard = _w == 2 && _ride ? 0.7f : 0f; _me.facing = Forward; }

            if (play && !VrOn)
            {
                var m = Act.LookMouse + Act.LookStick;
                _yaw += m.x * 2.2f;
                _pitch = Mathf.Clamp(_pitch - m.y * 2.2f, -85f, 85f);
            }
            if (play)
            {
                if (!_match && VrOn && Act.Info.Down) { _vrInfo = !_vrInfo; VrHudLean(_vrInfo); } // VR の練習場：X で表示を出し入れ
                if (!_match && VrOn)
                {
                    // VR の練習場：右スティックを上下に倒して武器を持ちかえる
                    float sy = Vr.RightStick.y;
                    if (Mathf.Abs(sy) > 0.8f && Mathf.Abs(Vr.RightStick.x) < 0.4f && !_stickHeld) { Select((_w + (sy > 0 ? Weapons.Length - 1 : 1)) % Weapons.Length); _stickHeld = true; }
                    if (Mathf.Abs(sy) < 0.3f) _stickHeld = false;
                }
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
            if (!_frozen) Move(play && _stunT <= 0, dt); // 地雷を踏んだ3秒は動けない
            Shots(dt);
            MinesTick(dt);
            WeaponTick(dt);
            CameraTick(dt);
            AimLineTick(play);
            HudTick();
        }

        bool _frozen;
        int _airJumps = 1; // 団扇の2段ジャンプの残り（地面に着くと 1 にもどる）
        /// <summary>練習場の立ち位置：城の前の段と大きな箱（x -57〜-47）より東。わら人形の手前。</summary>
        static Vector3 PracticeSpawn => WestCastle + new Vector3(21.5f, 0.3f, 0);

        // VR では歩く向きは頭の向き、目は頭の位置、ねらいは右手の向き
        float MoveYaw => VrOn ? _yaw + Vr.HeadYaw : _yaw;
        Vector3 Forward => Quaternion.Euler(0, MoveYaw, 0) * Vector3.forward;
        Vector3 Right => Quaternion.Euler(0, MoveYaw, 0) * Vector3.right;
        public Vector3 EyePos => VrOn && ReferenceEquals(Vr.Body, this) ? VrHead : _rig.transform.position + Vector3.up * Eye;
        public Vector3 Aim => VrOn ? Vr.RightAim.forward : Quaternion.Euler(_pitch, _yaw, 0) * Vector3.forward;

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
                else
                {
                    // 空中：ふつうの速さより速いとき（ワイヤー・跳ぶわざのあと）は、ゆっくりしか落とさない（勢いを残す。2026-11-18）
                    flat = Vector3.MoveTowards(flat, wish, (flat.magnitude > wish.magnitude + 1f ? 2.5f : 9f) * dt);
                    // 団扇：空中でもう一度だけ跳べる（2段ジャンプ）。地面に着いたら回復（ユーザーの指示）
                    if (play && _w == 5 && _airJumps > 0 && Act.Jump.Down)
                    {
                        _airJumps--; _vel.y = 9f; // 落ちている途中でも、しっかり上へ（7.5 → 9）
                        Play(_sJump, 0.5f); Swing(0.3f);
                        Sparks(_rig.transform.position + Vector3.up * 0.3f, new Color(1f, 0.95f, 0.5f), 8); // あおいだ風
                    }
                }
                if (_grounded) _airJumps = 1;
                _vel.x = flat.x; _vel.z = flat.z;
                _vel.y -= Gravity * dt;
            }
            var flags = _cc.Move(_vel * dt);
            _grounded = _cc.isGrounded;
            if (_grounded && _vel.y < 0) _vel.y = -2f;
            if ((flags & CollisionFlags.Above) != 0 && _vel.y > 0) _vel.y = 0;
            // 落ちたら天守閣の前へもどす
            if (_rig.transform.position.y < -19f) Teleport(_match ? SpawnOf(1, 0) : PracticeSpawn);
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
            if (VrOn && !_camOverride) return; // VR：カメラは頭に合わせて Vr が置く
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

        void Play(AudioClip c, float vol) { if (c != null && _audio != null) _audio.PlayOneShot(c, vol * SoundPrefs.Effects); } // メニューの「効果音」の大きさに合わせる

        // ───────── 表示（板） ─────────

        Board _hud, _pauseB;
        Text _wName, _hint, _kill, _gaugeLabel;
        RectTransform _gauge, _hpFill;
        Image _gaugeImg, _gaugeBg;
        readonly Text[] _wList = new Text[6];
        readonly List<(Text t, float life, Vector3 world)> _dmg = new List<(Text, float, Vector3)>();
        Text _wTag;

        void BuildHud()
        {
            _hud = Board.Create("SENGOKU の表示", new Vector2(1920, 1080), 40);
            _hud.Apply();
            var r = _hud.root;
            Board.Fans(r, new Rect(16, 16, 1888, 1048), new Color(1, 1, 1, 0.65f), 80);
            // 照準（VR では手の先の光の線でねらうので出さない）
            if (!Vr.On)
            {
                Board.Ring(r, new Vector2(960, 540), 16, new Color(1, 1, 1, 0.85f), false);
                var dot = Board.Panel(r, new Rect(957, 537, 6, 6), Board.Teal); dot.sprite = Board.DiscSprite;
            }
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
            _gaugeBg = Board.Panel(r, new Rect(860, 600, 200, 8), new Color(0.02f, 0.05f, 0.1f, 0.7f));
            _gaugeImg = Board.Panel(r, new Rect(860, 600, 200, 8), Board.Gold);
            _gauge = (RectTransform)_gaugeImg.transform;
            _gaugeLabel = Board.Label(r, new Rect(760, 612, 400, 26), "", 17, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            // 武器
            _wTag = Board.NameTag(r, new Rect(1810, 640, 56, 260), "二刀流", Board.Vermilion, true, 30);
            _wName = Board.Display(r, new Rect(1200, 830, 590, 70), "", 44, Color.white, TextAnchor.MiddleRight);
            for (int i = 0; i < 6; i++)
                _wList[i] = Board.Label(r, new Rect(1440, 640 + i * 30, 350, 28), "", 18, Color.white, TextAnchor.MiddleRight);
            _hint = Board.Label(r, new Rect(980, 900, 810, 140), "", 18, new Color(1, 1, 1, 0.85f), TextAnchor.UpperRight);
            if (Vr.On) Board.Label(r, new Rect(60, 1010, 1300, 30), _match ? VrKeys : VrKeysPractice, 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
            else Board.Label(r, new Rect(60, 1010, 1300, 30), _match ? "WASD 移動　Shift 走る　Space ジャンプ　E（Q・ホイール押しこみでも）移動わざ　F 殴る（大将落としは近接だけ）　Esc 一時停止　しばらく攻撃を受けなければ回復" : "WASD 移動　Shift 走る　Space ジャンプ　E（Q・ホイール押しこみでも）移動わざ　1〜6・ホイール 武器　Esc 一時停止", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
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
            _hint.text = VrText(w.hint);
            _kill.text = _match ? "" : $"倒したわら人形　{_kills}";
            MatchHudTick();
            _hpFill.sizeDelta = new Vector2(416 * _hp / 100f, 16);
            float g = Gauge01;
            _gauge.sizeDelta = new Vector2(200 * g, 8);
            _gaugeImg.color = g >= 1 ? Board.Teal : Board.Gold;
            _gaugeLabel.text = GaugeText;
            float udt = Time.unscaledDeltaTime;
            for (int i = 0; i < _dmg.Count; i++)
            {
                var (t, life, world) = _dmg[i];
                if (life <= 0) { if (t.text != "") t.text = ""; continue; }
                life -= udt; world += Vector3.up * udt * 1.2f;
                _dmg[i] = (t, life, world);
                var sp = _cam != null ? HudUi(world) : Vector3.back;
                if (sp.z <= 0) { t.text = ""; continue; }
                var rt = (RectTransform)t.transform;
                rt.anchoredPosition = new Vector2(sp.x + 960 - 100, -(540 - sp.y) + 25);
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
                Board.ButtonNode(r, new Rect(160, 285, 400, 56), (_match ? "降参して島へもどる" : "島へもどる") + "\n<size=15>" + (Vr.On ? "Y" : "Esc") + " を長押しでも</size>", 22, Surrender);
            }
            _pauseB.Alpha = 1;
        }

        /// <summary>降参して島へもどる（練習場は、そのまま島へ）。一時停止の板のボタンか、Y・Esc の長押しで。</summary>
        void Surrender()
        {
            if (_match) { if (!Ts.ShotMode) Kassen.RewardSengoku(false, _level); Hud.Toast("SENGOKU：また挑戦してね"); }
            else Hud.Toast($"SENGOKU 練習場：わら人形を {_kills} 体倒しました");
            Leave();
        }

        float _quitHold;

        /// <summary>Y（VR）・Esc を 1.5 秒長押ししたら、降参して島へもどる（2026-11-18 ユーザーの指示）。もどったら true。</summary>
        bool QuitHoldTick()
        {
            if (Ts.ShotMode) return false;
            if (!Act.Back.Held) { _quitHold = 0; return false; }
            _quitHold += Time.unscaledDeltaTime;
            if (_quitHold > 0.4f && _quitHold < 1.5f) Center(_match ? "降参する…" : "島へもどる…", Board.Vermilion, 0.2f, $"{(Vr.On ? "Y" : "Esc")} をそのまま押しつづける（{1.5f - _quitHold:0.0} 秒）");
            if (_quitHold < 1.5f) return false;
            _quitHold = -999f;
            if (_paused) Resume();
            Surrender();
            return true;
        }

        void Resume()
        {
            _paused = false; Time.timeScale = 1f;
            if (_pauseB != null) _pauseB.Alpha = 0;
        }
    }
}
