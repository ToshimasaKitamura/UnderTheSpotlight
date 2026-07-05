# ゲーム全体仕様書

## ゲーム概要

**タイトル:** アンダーザスポットライト（Under the Spotlight）

**ジャンル:** ターン制カードバトル × アクションコマンド × 弾幕STG

**コンセプト:**
- 最大4人のキャラクターでパーティを編成
- カード選択 → 部位選択 → アクションコマンド → 弾幕回避のフェーズ制バトル
- レトロな8bitピクセルアート風グラフィック
- ホットシート方式（1台のデバイスで交代プレイ）

## プレイヤー数

- **基本人数:** 4人
- **可変人数:** 1〜4人（GameTuningで調整可能）
- **プレイ形式:** ホットシート（ローカルマルチプレイ）

## ゲームフロー

### メインループ
```
タイトル
  ↓
ワールドマップ（ステージ選択）
  ↓
キャラクター選択（4人編成）
  ↓
バトル（複数ボス戦）
  ├─ カード選択フェーズ
  ├─ 部位選択フェーズ
  ├─ アクションコマンドフェーズ
  └─ 弾幕回避フェーズ
  ↓
リザルト
  ↓
ワールドマップに戻る
```

### バトルフェーズ詳細

#### 1. カード選択フェーズ (CardSelectPhase)
- **目的:** 各プレイヤーがアクションカードを1枚選ぶ
- **制限時間:** 15秒（デフォルト、tuningで変更可）
- **手札枚数:** 3枚（デッキから引く）
- **ターン順:** ランダムまたは固定（tuningで設定）
- **UI:** 縦並びカード表示、選択中はハイライト

#### 2. 部位選択フェーズ (PartSelectPhase)
- **目的:** 攻撃対象の敵部位を選ぶ
- **条件:** 攻撃カードかつ全体攻撃以外
- **スキップ:** 非攻撃カード、全体攻撃、選択肢1つの場合
- **制限時間:** 12秒（デフォルト）
- **UI:** 敵のヒットボックス表示、部位ごとにHP表示

#### 3. アクションコマンドフェーズ (ActionCommandPhase)
- **目的:** ミニゲームでダメージ倍率を決定
- **ラウンド数:** 3ラウンド（デフォルト）
- **難易度:** ラウンドごとに上昇
- **成功判定:** タイミング、連打、長押し、シーケンスなど
- **報酬:** 成功数に応じてダメージ倍率上昇

**ミニゲームの種類:**
- **Timing** - タイミング押し
- **Mash** - 連打
- **Hold** - 長押し
- **Sequence** - シーケンス入力
- **GroupButton** - グループボタン
- **Guard** - ガード
- **Hammer** - ハンマー
- **Jump** - ジャンプ
- **Spin** - スピン

#### 4. 弾幕回避フェーズ (DodgePhase)
- **目的:** 敵の弾幕を回避して生き残る
- **操作:** キーボード（WASD/矢印キー）で自機を移動
- **プレイヤー交代:** ターン制で1人ずつ操作
- **HP:** 全プレイヤー共有（デフォルト5、tuningで変更可）
- **終了条件:** 全員のターン終了 or HP0

**弾幕システム:**
- 敵ごとに弾幕パターンを定義（BossDanmakuPattern）
- 移動パターンも定義可能（BossMovementPattern）
- 当たり判定: 自機と弾のコリジョン検出

## キャラクターシステム

### キャラクターデータ (CharacterData)
各キャラクターは以下の情報を持つ：
- **displayName:** 表示名
- **sprite:** キャラクター画像
- **initialDeck:** 初期デッキ（CardData配列）
- **color:** プレイヤーカラー（UI表示用）
- **description:** キャラクター説明

### 登場キャラクター
1. **イルカ** (Iruka)
2. **ネコ** (Neko)
3. **ヒツジ** (Hitsuji)
4. **ニシカワ** (Nishikawa)
5. **ラーテル** (Ratel)
6. **ピエロ** (Pierrot)
7. **アクマ** (Akuma)
8. **スガク先生** (SugakuSensei)

## カードシステム

### カードカテゴリ (CardCategory)
- **Attack:** 攻撃カード（敵にダメージ）
- **Defend:** 防御カード
- **Support:** サポートカード
- **Special:** 特殊カード

### カード攻撃範囲 (CardAttackRange)
- **Single:** 単体（1部位）
- **Upper:** 上半身全体
- **Lower:** 下半身全体
- **All:** 全体攻撃

### カードデータ (CardData)
- **displayName:** カード名
- **category:** カテゴリ
- **attackRange:** 攻撃範囲
- **basePower:** 基礎攻撃力
- **cost:** コスト（未使用）
- **description:** 説明文

### デッキシステム
- **DeckDefinition:** デッキ構成定義（カード種×枚数）
- **PlayerDeck:** ランタイムデッキ（山札/手札/捨札/強化Lv）
- **PartyDeckController:** パーティ全員のデッキ管理
- **ドロー枚数:** 3枚（デフォルト、tuningで変更可）

## 敵システム

### 敵データ (EnemyData)
- **displayName:** 敵名
- **sprite:** 敵画像
- **maxHp:** 最大HP
- **parts:** 部位リスト（EnemyPart[]）
- **danmakuPattern:** 弾幕パターン
- **movementPattern:** 移動パターン

### 部位システム (EnemyPart)
- **id:** 部位ID（ユニーク）
- **displayName:** 部位名
- **maxHp:** 部位HP
- **position:** 攻撃範囲（Upper/Lower/Core）
- **telegraphDialogue:** 予告セリフ（この部位を攻撃する予告）
- **破壊時:** 部位HPが0になると破壊（選択不可）

### ランタイム状態 (EnemyRuntimeState)
- 部位ごとの現在HP
- 破壊済み部位の記録
- 次の予告部位の管理

## UIシステム

### 8bitピクセル風デザイン
- **PixelFontRenderer:** カスタムピクセルフォント描画
- **美咲ゴシック:** 日本語フォント（8x8ピクセル）
- **GameColorScheme:** 統一カラーパレット
- **OnGUI:** IMGUIベースのUI描画

### UI要素
- **SharedHpGaugeUI:** 共有HPバー（弾幕フェーズのみ表示）
- **BombGaugeUI:** ボムゲージ
- **DanmakuFrameView:** 弾幕フレーム装飾
- **PlayerSwitchAnnouncer:** プレイヤー切替アナウンス
- **SpeechBubble:** セリフ吹き出し
- **CardHandView:** カード手札表示

### プレイヤーカラー
4人のプレイヤーをカラーで区別：
1. **赤系** (1f, 0.35f, 0.35f)
2. **青系** (0.35f, 0.6f, 1f)
3. **緑系** (0.35f, 1f, 0.4f)
4. **黄系** (1f, 0.85f, 0.35f)

## シーン構成

### 1. TitleScene
- タイトル画面
- スタートボタン → WorldMapSceneへ

### 2. WorldMapScene
- ステージ選択
- 進行状況表示（RunProgress）
- ステージ選択 → CharacterSelectSceneへ

### 3. CharacterSelectScene
- キャラクター選択（最大4人）
- スポットライト演出
- 決定 → BattleSceneへ

### 4. BattleScene
- バトルフェーズ実行
- ステージ内の複数ボス戦
- 勝利 → 次のボス or ResultSceneへ
- 敗北 → ResultSceneへ

### 5. ResultScene
- 勝敗表示
- WorldMapSceneへ戻る

## シーントランジション

### エフェクトの種類
- **Fade:** フェードイン/アウト（Title → WorldMap）
- **Slide:** スライド遷移（WorldMap → CharacterSelect）
- **Wipe:** ワイプ遷移（CharacterSelect → Battle）

### 実装
- **SceneTransitionManager:** シングルトン、遷移管理
- **Canvas:** フルスクリーンOverlay
- **AsyncOperation:** 非同期シーンロード

## データ管理

### GameTuning
ゲーム全体の調整値を一元管理：
- **playerCount:** プレイヤー人数
- **drawCount:** ドロー枚数
- **selectTimeLimit:** 選択制限時間
- **maxHp:** 最大HP
- **roundsPerActionCommand:** ACラウンド数
- **baseTimeLimit, timeLimitPerRound:** AC難易度パラメータ
- **playerColors:** プレイヤーカラー配列

### RunProgress
ステージ進行状況を記録：
- 現在のステージ
- 現在のボスインデックス
- クリア済みステージ
- ボス撃破時の処理

### GameSceneManager
シーン間のデータ受け渡し：
- 選択されたステージ
- 選択されたキャラクターパーティ
- 現在のボスインデックス

## 技術仕様

### Unity バージョン
- Unity 2022.3 LTS 以降推奨

### パッケージ
- **Universal RP:** 2Dレンダリング
- **Input System:** 新しい入力システム
- **TextMesh Pro:** テキスト描画（一部使用）

### アーキテクチャ
- **ステートマシン:** BattleStateMachine（フェーズ管理）
- **Singleton:** GameSceneManager, SceneTransitionManager
- **ScriptableObject:** データ定義（Card, Character, Enemy, Stage）
- **イベント駆動:** Action, event による疎結合

### 入力
- **キーボード:** 主要操作
- **対応キー:**
  - 矢印キー / WASD: 移動・選択
  - Enter / Space / Z: 決定
  - Escape: キャンセル（一部）

## ゲームバランス設計方針

### 難易度調整ポイント
1. **カード選択制限時間:** 短いほど判断が難しい
2. **ACラウンド数:** 多いほど難しい
3. **AC難易度パラメータ:** speedRamp, timeLimit減少
4. **弾幕パターン:** 密度、速度、パターン複雑さ
5. **共有HP:** 少ないほど厳しい
6. **敵部位HP:** 高いほどバトルが長引く

### 進行設計
- ステージごとに難易度上昇
- ステージ内で複数ボス戦（徐々に強化）
- カード強化システム（PlayerDeck.UpgradeLevel、未実装）

## 拡張予定機能

以下は現在未実装または将来追加予定：
- **カード強化システム:** デッキ強化レベルに応じた効果
- **アイテムシステム:** ドロー枚数増加、HP回復など
- **ボムシステム:** 弾消し、無敵時間
- **マルチプレイヤー:** ネットワーク対戦
- **セーブ/ロード:** 進行状況保存
- **サウンド:** BGM、SE
- **アニメーション:** キャラクター、敵の動き
- **ストーリー:** 会話イベント、カットシーン

## 制約事項

### 現在の制限
- ホットシート専用（ネットワーク非対応）
- セーブ機能なし（ランのみ）
- サウンド未実装
- キャラクターアニメーション最小限

### パフォーマンス
- ターゲット: 60 FPS
- 解像度: 1920x1080想定
- 弾幕最大数: 調整中（パターンによる）

## 更新履歴

- **2024-06-20:** 初版作成
  - フォルダ整理完了後の仕様書作成
  - 既存実装から仕様を抽出
