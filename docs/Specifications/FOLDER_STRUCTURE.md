# フォルダ構成

このドキュメントは、プロジェクトのフォルダ構成を説明します。

## Assets フォルダ構成

```
Assets/
├── Data/                      # ゲームデータ（ScriptableObject）
│   ├── Cards/                # カードデータ
│   ├── Characters/           # キャラクターデータ（名前付き）
│   │   ├── Iruka.asset      # イルカ
│   │   ├── Neko.asset       # ネコ
│   │   ├── Hitsuji.asset    # ヒツジ
│   │   ├── Nishikawa.asset  # ニシカワ
│   │   ├── Ratel.asset      # ラーテル
│   │   ├── Pierrot.asset    # ピエロ
│   │   ├── Akuma.asset      # アクマ
│   │   └── SugakuSensei.asset # スガク先生
│   ├── csv/                 # CSVデータ
│   ├── Enemies/             # 敵データ
│   └── Stages/              # ステージデータ
├── Editor/                   # エディター拡張スクリプト
│   ├── CharacterSelectSceneSetup.cs
│   └── CsvImporter.cs
├── Images/                   # 画像・スプライト
├── Prefabs/                  # プレハブ
├── Resources/                # 動的読み込み用リソース
│   └── Fonts/               # フォント（美咲ゴシック）
├── Scenes/                   # シーンファイル
│   ├── TitleScene.unity
│   ├── WorldMapScene.unity
│   ├── CharacterSelectScene.unity
│   ├── BattleScene.unity
│   └── ResultScene.unity
├── Settings/                 # プロジェクト設定
│   ├── UniversalRP.asset
│   ├── Renderer2D.asset
│   └── Scenes/
├── Src/                      # ソースコード（カテゴリ別整理）
│   ├── Battle/              # バトルシステム（3ファイル）
│   │   ├── BattleSceneController.cs
│   │   ├── BattleStateMachine.cs
│   │   └── IBattlePhase.cs
│   ├── Card/                # カードシステム（9ファイル）
│   │   ├── CardData.cs
│   │   ├── CardEnums.cs
│   │   ├── CardHandView.cs
│   │   ├── CardInstance.cs
│   │   ├── CardSelectPhase.cs
│   │   ├── CardSelectStage.cs
│   │   ├── DeckDefinition.cs
│   │   ├── PartyDeckController.cs
│   │   └── PlayerDeck.cs
│   ├── Character/           # キャラクター関連（5ファイル）
│   │   ├── CharacterCardView.cs
│   │   ├── CharacterData.cs
│   │   ├── CharacterSelectSceneController.cs
│   │   ├── CharacterSelectView.cs
│   │   └── CharacterSpotlightView.cs
│   ├── Dodge/               # 回避フェーズ（7ファイル）
│   │   ├── DodgeBombManager.cs
│   │   ├── DodgeEnemyManager.cs
│   │   ├── DodgeHealthManager.cs
│   │   ├── DodgePhase.cs
│   │   ├── DodgePhaseUtils.cs
│   │   ├── DodgePlayerManager.cs
│   │   └── DodgeVisuals.cs
│   ├── Enemy/               # 敵システム（6ファイル）
│   │   ├── BossDanmakuPattern.cs
│   │   ├── BossMovementPattern.cs
│   │   ├── EnemyData.cs
│   │   ├── EnemyOnFieldController.cs
│   │   ├── EnemyOnFieldSpawner.cs
│   │   └── EnemyRuntimeState.cs
│   ├── MicroGame/           # ミニゲーム（16ファイル）
│   │   ├── ActionCommandModifiers.cs
│   │   ├── ActionCommandPhase.cs
│   │   ├── AcUi.cs
│   │   ├── GroupButtonMicroGame.cs
│   │   ├── GuardMicroGame.cs
│   │   ├── HammerMicroGame.cs
│   │   ├── HoldMicroGame.cs
│   │   ├── IMicroGame.cs
│   │   ├── JumpMicroGame.cs
│   │   ├── MashMicroGame.cs
│   │   ├── MicroGameIMGUIHelper.cs
│   │   ├── MicroGameRegistry.cs
│   │   ├── MiniGameStartView.cs
│   │   ├── SequenceMicroGame.cs
│   │   ├── SpinMicroGame.cs
│   │   └── TimingMicroGame.cs
│   ├── System/              # システム・シーン管理（8ファイル）
│   │   ├── GameSceneManager.cs
│   │   ├── ResultSceneController.cs
│   │   ├── SceneTransitionManager.cs
│   │   ├── TitleSceneController.cs
│   │   ├── TitleView.cs
│   │   ├── WorldMapPhase.cs
│   │   ├── WorldMapSceneController.cs
│   │   └── WorldMapView.cs
│   ├── UI/                  # UIコンポーネント（11ファイル）
│   │   ├── ActivePlayerRing.cs
│   │   ├── BattleBackgroundView.cs
│   │   ├── BombGaugeUI.cs
│   │   ├── DanmakuFrameView.cs
│   │   ├── PartSelectPhase.cs
│   │   ├── PartSelectView.cs
│   │   ├── PlayerSelectionListView.cs
│   │   ├── PlayerSwitchAnnouncer.cs
│   │   ├── PlayerUICircle.cs
│   │   ├── SharedHpGaugeUI.cs
│   │   └── SpeechBubble.cs
│   └── Utils/               # ユーティリティ（19ファイル）
│       ├── BombController.cs
│       ├── Bullet.cs
│       ├── DanmakuManager.cs
│       ├── DebugMultiPlayerInput.cs
│       ├── FloatingCharacterMotion.cs
│       ├── GameColorScheme.cs
│       ├── GameFont.cs
│       ├── GameTuning.cs
│       ├── IPlayerOnFieldInput.cs
│       ├── KeyboardPlayerOnFieldInput.cs
│       ├── PixelFontRenderer.cs
│       ├── PlayerOnFieldController.cs
│       ├── PlayerOnFieldHittable.cs
│       ├── PlayerTurnManager.cs
│       ├── PulseAlpha.cs
│       ├── RunProgress.cs
│       ├── SharedHpController.cs
│       ├── StageData.cs
│       └── WorldMapData.cs
└── TextMesh Pro/            # TextMeshPro（Unity標準パッケージ）

## 整理内容

### 削除したファイル・フォルダ
- `.DS_Store` - macOSの隠しファイル
- `_Recovery/` - 復旧用の一時フォルダ
- `Scenes/SampleScene.unity` - 使用されていないサンプルシーン
- `Fonts/` - Resources/Fontsに統一（重複解消）
- `Fonts/misaki_font.zip` - 解凍済みのため不要
- `Fonts/misaki.txt`, `readme.txt` - 不要なテキストファイル

### 改名したファイル
CharacterDataを番号からキャラクター名に変更：
- `0.asset` → `Iruka.asset` （イルカ）
- `1.asset` → `Neko.asset` （ネコ）
- `2.asset` → `Hitsuji.asset` （ヒツジ）
- `3.asset` → `Nishikawa.asset` （ニシカワ）
- `4.asset` → `Ratel.asset` （ラーテル）
- `5.asset` → `Pierrot.asset` （ピエロ）
- `6.asset` → `Akuma.asset` （アクマ）
- `7.asset` → `SugakuSensei.asset` （スガク先生）

### Srcフォルダの整理
84個のC#ファイルを機能別に9つのサブフォルダに整理：
- **Battle/** - バトルステートマシンとフェーズ管理
- **Card/** - カードシステム、デッキ管理
- **Character/** - キャラクター選択、表示
- **Dodge/** - 回避フェーズ（弾幕）の各種マネージャー
- **Enemy/** - 敵データ、弾幕パターン
- **MicroGame/** - アクションコマンドのミニゲーム
- **System/** - シーン管理、トランジション
- **UI/** - UI表示コンポーネント
- **Utils/** - 汎用ユーティリティ、データ構造

## ビルド設定
以下のシーンが順番にビルドに含まれます：
1. TitleScene
2. WorldMapScene
3. CharacterSelectScene
4. BattleScene
5. ResultScene
