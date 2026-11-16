# ツクヨミ

月の下に広がる仮想空間。Unity 6（6000.1.1f1・URP）で作っています。
ミニヤチヨのスマコンの右クリックか、彩葉のスマホの「ツクヨミ」アプリから入ります。

## いまできること（段階1：土台）

- **海の大鳥居**から始まる（初回）。2回目からは、前回ログアウトした場所から始まる
- 海の回廊 → 大門 → **月見商店街**（大通り・東西通り・横丁・裏路地。入り組んだ町並み）
- **大画面広場**：ヤチヨの配信が大画面に流れている（踊るヤチヨ・流れるコメント・同時接続数）
- **釣り堀と茶屋**（釣りは段階3で）、**天の川**と3本の反り橋、**千年桜の丘**（花びらが舞う）
- **ライブステージ**（いまは幕が下りている。ライブは段階4で）、**月の御殿**
- 空は**現実の時刻**とつながっている（東京の太陽の位置。夜は星と大きな月、灯籠と提灯がともる）
- M で**地図とワープ**、Esc でメニュー（つづける・地図・ログアウト）

### そうさ

| キー | すること |
|---|---|
| WASD・矢印 | 歩く（Shift で走る） |
| Space | ジャンプ |
| マウス | 見回す（画面をクリックすると操作開始。ホイールで近づく・離れる） |
| M | 地図とワープ |
| Esc | メニュー・ログアウト |

## これからの予定

1. ~~土台~~（できた）
2. 商店街と買い物：お店・にぎやかな人たち・ふじゅ〜（ミニヤチヨの本物の残高）で支払い・着替え
3. 釣り堀と休憩スペース：釣り・魚の図鑑・ランキング（仮想）・食べる
4. ライブ：ヤチヨのオリジナル曲（歌詞は字幕）・演出・派手な舞台装置

## しくみ

- 世界は `Assets/Tsukuyomi/Editor/WorldBuilder.cs` がまるごと組み立てて `Assets/Scenes/Tsukuyomi.unity` に保存する（乱数の種が固定なので毎回同じ形）。部品は `Parts.cs`、屋根や丘の形は `Kit.cs`
- 動きは `Assets/Tsukuyomi/Scripts`：自分（`PlayerController`・`OrbitCamera`）、空（`RealTimeSky`）、画面の表示（`Hud`）、保存（`GameSession`・`SaveStore`）、配信（`StreamStudio`）、人形（`Doll`・`DollAnimator`）
- 見た目のシェーダーは `Assets/Tsukuyomi/Shaders`（水・光る面・星・月・花びら・大画面）
- 保存先：`%APPDATA%\Tsukuyomi\save.json`（環境変数 `TSUKUYOMI_DATA` で変えられる）

### exe の作り方

```bat
"C:\Program Files\Unity\Hub\Editor\6000.1.1f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Users\cs25052\Tsukuyomi -executeMethod Tsukuyomi.EditorTools.BuildScript.Build -logFile build.log
```

出来上がりは `Build\Tsukuyomi.exe`（環境変数 `TSUKUYOMI_BUILD_OUT` で変えられる）。
本番は `dist\Tsukuyomi.exe`。ミニヤチヨはここを起動する（`TSUKUYOMI_EXE` で変えられる）。
本番に反映するときは、ツクヨミを閉じてから `Build` の中身を `dist` にコピーする。

### 動作確認用の起動オプション

| オプション | 意味 |
|---|---|
| `--from contact` / `--from iroha` | どこから来たか（あいさつが変わる） |
| `--time 21:30` | 時刻をずらして試す |
| `--warp shopping` | その場所から始める（torii・shopping・plaza・fishing・river・sakura・live・palace） |
| `--cam 180,10,8` | カメラの向き（左右, 上下, 距離） |
| `--fresh` | 保存した場所を使わない |
| `--shot a.png --shot-delay 6` | 数秒後に画面を保存して終わる（場所は保存しない） |

窓で試すときは Unity の `-screen-fullscreen 0 -screen-width 1280 -screen-height 720` を足す。

## 絵や形について

建物・人形・空・水はすべて、基本の形の組み合わせとプログラムで作っています（外から取ってきた素材はまだ使っていません）。
ヤチヨの色はミニヤチヨの絵（幻影石さんの作品）を参考にしています。個人で楽しむためだけに使い、配布はしません。
