# バトルシーン仕様確認チェックリスト

## 実行日: 2026-06-20

このドキュメントは、BattleSceneSetup.csで作成されたシーンが仕様書通りか確認するためのチェックリストです。

---

## セットアップ手順

### 1. エディタースクリプト実行
- [ ] Unityエディタを開く
- [ ] メニューバー → `Tools` → `Setup Battle Scene` を実行
- [ ] コンソールに成功メッセージが表示されることを確認

---

## 仕様確認項目

### A. シーン基本構成

#### BattleManager（ルートGameObject）
- [ ] `BattleManager` GameObjectが存在する
- [ ] 以下のコンポーネントがアタッチされている：
  - [ ] `BattleStateMachine` (initialState = CardSelect)
  - [ ] `BattleSceneController`
  - [ ] `PartyDeckController` (playerCount = 4, drawCount = 3)
  - [ ] `EnemyRuntimeState`
  - [ ] `SharedHpController` (maxHp = 5)
  - [ ] `RunProgress`

#### Phases（フェーズコンテナ）
- [ ] `Phases` GameObjectが `BattleManager` の子として存在
- [ ] 以下の4つのフェーズが子オブジェクトとして存在：
  - [ ] `CardSelectPhase` (playerCount = 4, selectTimeLimit = 15秒)
  - [ ] `PartSelectPhase` (playerCount = 4, selectTimeLimit = 12秒)
  - [ ] `ActionCommandPhase` (playerCount = 4, roundsPerActionCommand = 3)
  - [ ] `DodgePhase`

#### Players（プレイヤー配置）
- [ ] `Players` コンテナが存在する
- [ ] 以下の4つのプレイヤーオブジェクトが存在：
  - [ ] `Player1` (Tag: Player, Position: (-6, -3, 0))
  - [ ] `Player2` (Tag: Player, Position: (-2, -3, 0))
  - [ ] `Player3` (Tag: Player, Position: (2, -3, 0))
  - [ ] `Player4` (Tag: Player, Position: (6, -3, 0))
- [ ] 各プレイヤーに `SpriteRenderer` がアタッチされている
- [ ] 各プレイヤーの色が正しい：
  - [ ] Player1: 赤系 (1f, 0.35f, 0.35f)
  - [ ] Player2: 青系 (0.35f, 0.6f, 1f)
  - [ ] Player3: 緑系 (0.35f, 1f, 0.4f)
  - [ ] Player4: 黄系 (1f, 0.85f, 0.35f)
- [ ] 各プレイヤーのスプライトが設定されている
  - [ ] CharacterSelectSceneで選択されたキャラクター、またはデフォルトキャラクターのスプライト
  - [ ] Player.png スプライトシート（Player_0〜Player_7）から取得

#### Enemy（敵配置）
- [ ] `Enemy` GameObjectが存在する (Tag: Enemy, Position: (0, 3, 0))
- [ ] Enemyルートに `SpriteRenderer` がアタッチされている
- [ ] `Visual` 子オブジェクトが存在する
- [ ] Visual子にも `SpriteRenderer` がアタッチされている（PartSelectPhaseで使用）

#### UI要素
- [ ] `UI` コンテナが存在する
- [ ] 以下のUI要素が子として存在：
  - [ ] `SharedHpGaugeUI` (SharedHpGaugeUIコンポーネント)
  - [ ] `DanmakuFrameView` (DanmakuFrameViewコンポーネント)

#### Camera設定
- [ ] `Main Camera` が存在する
- [ ] 以下の設定が正しい：
  - [ ] Position: (0, 0, -10)
  - [ ] Orthographic: true
  - [ ] Orthographic Size: 8
  - [ ] Background Color: 濃いグレー/紫系 (0.1f, 0.1f, 0.15f)

---

## 仕様書との対応確認

### バトルフェーズ (GAME_SPECIFICATION.md 42-87行)

#### 1. カード選択フェーズ
- [x] 制限時間: 15秒 → CardSelectPhase.selectTimeLimit = 15f
- [x] 手札枚数: 3枚 → PartyDeckController.drawCount = 3
- [x] プレイヤー数: 4人 → CardSelectPhase.playerCount = 4

#### 2. 部位選択フェーズ
- [x] 制限時間: 12秒 → PartSelectPhase.selectTimeLimit = 12f
- [x] プレイヤー数: 4人 → PartSelectPhase.playerCount = 4

#### 3. アクションコマンドフェーズ
- [x] ラウンド数: 3 → ActionCommandPhase.roundsPerActionCommand = 3
- [x] プレイヤー数: 4人 → ActionCommandPhase.playerCount = 4

#### 4. 弾幕回避フェーズ
- [x] 共有HP: 5 → SharedHpController.maxHp = 5
- [x] フェーズコンポーネント: DodgePhase存在

### プレイヤーカラー (GAME_SPECIFICATION.md 175-180行)
- [x] Player1: 赤系 (1f, 0.35f, 0.35f)
- [x] Player2: 青系 (0.35f, 0.6f, 1f)
- [x] Player3: 緑系 (0.35f, 1f, 0.4f)
- [x] Player4: 黄系 (1f, 0.85f, 0.35f)

### タグシステム
- [x] プレイヤーオブジェクト: `Player` タグ
- [x] 敵オブジェクト: `Enemy` タグ

---

## 次のステップ（セットアップ後の作業）

以下は自動セットアップでは完了しない項目です。手動で設定が必要です：

### 1. GameTuningアセット設定
- [ ] GameTuningアセットを作成（未作成の場合）
- [ ] BattleManagerの各コンポーネントに設定

### 2. スプライト設定
- [ ] Player1〜4に適切なキャラクタースプライトを設定
- [ ] Enemyに適切な敵スプライトを設定

### 3. EnemyData設定
- [ ] EnemyDataアセットを作成
- [ ] EnemyRuntimeStateに設定

### 4. DeckDefinition設定
- [ ] DeckDefinitionアセットを作成
- [ ] PartyDeckControllerに設定

### 5. ビルド設定確認
- [ ] ProjectSettings/EditorBuildSettings.asset に BattleScene.unity が登録されているか確認

---

## 問題が発生した場合

### セットアップスクリプトが見つからない
→ `Assets/Editor/BattleSceneSetup.cs` が存在するか確認

### メニュー項目が表示されない
→ Unityエディタを再起動

### コンポーネントが見つからないエラー
→ 以下のスクリプトが全て存在するか確認：
- BattleStateMachine.cs
- BattleSceneController.cs
- PartyDeckController.cs
- EnemyRuntimeState.cs
- SharedHpController.cs
- RunProgress.cs
- CardSelectPhase.cs
- PartSelectPhase.cs
- ActionCommandPhase.cs
- DodgePhase.cs
- SharedHpGaugeUI.cs
- DanmakuFrameView.cs

---

## 検証結果

**日付:** _______________________

**検証者:** _______________________

**結果:**
- [ ] 全てのチェック項目をクリア
- [ ] 一部未完了（下記に記載）
- [ ] 仕様とのずれを検出（下記に記載）

**備考:**

```
（ここに問題点や気づいた点を記載）
```

---

**このチェックリストは Docs/DEVELOPMENT_GUIDE.md の「実装後の照合」ステップに従って作成されています。**
