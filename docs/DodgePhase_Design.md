# DodgePhase 設計仕様書

戦闘中の **回避フェーズ（弾幕モード）** の Unity 実装仕様。
バイブコーディング時にこのドキュメントを最初に参照すること。

---

## 1. 概要

4人パーティのうち1人だけが「自機（白い球）」を操作してボスの弾幕を回避するフェーズ。
- 操作権は **秒数で自動ローテーション**（P1→P2→P3→P4）
- HP は **4人で1本を共有**
- 被弾で HP 減少、**HP=0 でゲームオーバー**
- 全員が **1周回ったら自動でフェーズ終了**

---

## 2. シーン構成（永続オブジェクト）

| GameObject | 主要コンポーネント | 役割 |
|---|---|---|
| `BattleStateMachine` | `BattleStateMachine` | フェーズ切替 |
| `DodgePhase` | `DodgePhase` | 回避フェーズの統制 |
| `Enemies` (root) | (Transformのみ) | 敵の親 |
| `Field` | `SpriteRenderer`(黒) | 中央の弾幕フィールド |
| `DanmakuManager` | `DanmakuManager` | 弾管理 |
| `Player×4` | `Player.prefab` インスタンス | 味方キャラ（側退避先） |

### Sorting Layer 順（下→上）
`Default → BackGround → Player → Enemy → Field → UI`

### Field レイヤー内 Order
| Order | 対象 |
|---|---|
| 0 | 中央フィールド (黒) |
| 1 | EnemyOnField (ボス) |
| 2 | PlayerOnField (白い球) |
| 3 | Bullets (薄赤い丸) |
| 4 | ActivePlayerRing |

---

## 3. プレハブ

| Prefab | 用途 | 主スプライト | デフォルト Visual Scale |
|---|---|---|---|
| `Player.prefab` | 味方キャラ4人（側） | `AllChara_X`（インスタンス毎差替） | 512 |
| `Enemy.prefab` | 敵 | (各種) | - |
| `EnemyOnField.prefab` | 弾幕中のボス | `AllChara_15` | 150 |
| `PlayerOnField.prefab` | 弾幕中の自機（白い球） | Knob (内蔵) | 150 |
| `BulletOnField.prefab` | 弾幕の弾 | Knob (内蔵, 薄赤色) | 100 |

---

## 4. クラス構成

### 4.1 ライフサイクル抽象
- **`IBattlePhase`** — `Enter / Exit / Tick`
- **`BattleStateMachine`** — フェーズの切替（CardSelect / ActionCommand / Dodge）

### 4.2 DodgePhase（オーケストレーター）

`IBattlePhase` 実装。`Enter()` で全シーケンスを開始、`EndDodge()` で巻き戻し。

#### Enter シーケンス
1. 中央フィールド境界を捕捉して `DanmakuManager.fieldBounds` に登録（弾クリッピング用）
2. 敵が上昇
3. プレイヤーが下に消えて縮小 → 画面端から小さく入場
4. EnemyOnField を上から降下（`SpawnAndEnterEnemyOnField`）
5. ボスに `BossDanmakuPattern` + `BossMovementPattern` を attach、StartPattern
6. 中央フィールドがスケールアップ
7. 自機（白い球）をフィールド下中央にスポーン
8. `SharedHpController` / `SharedHpGaugeUI` / `ActivePlayerRing` / `PlayerSwitchAnnouncer` / `PlayerTurnManager` を起動

#### EndDodge シーケンス
1. ボス弾幕停止 + 残弾全消去 + クリッピング解除
2. 自機/HP/Ring/Announcer/TurnMgr を破棄
3. EnemyOnField が上に飛んで破棄
4. 中央フィールドがフェードアウト + 上に移動
5. プレイヤー復帰
6. 敵が画面外上空からスナップして降下

### 4.3 自機系

| Class | 責務 |
|---|---|
| `IPlayerOnFieldInput` | 入力ソース抽象。`Vector2 ReadMovement()` |
| `KeyboardPlayerOnFieldInput` | キーボード入力（playerIndex 指定）— 現状未使用 |
| `DebugMultiPlayerInput` | デバッグ用：1〜4 を押すとそのプレイヤー入力。`turnSource.CurrentActivePlayer` と一致時のみ通す |
| `PlayerOnFieldController` | `IPlayerOnFieldInput` から取得した入力で移動。`movementBounds` でクランプ |
| `PlayerOnFieldHittable` | 被弾判定。`Hit` イベント発火、`invulnerabilityDuration` 中は再発火しない |

### 4.4 ターン制御

| Class | 責務 |
|---|---|
| `PlayerTurnManager` | 一定秒で `ActivePlayerChanged`、最後のターン終了で `RoundCompleted` 発火しコルーチン停止 |
| `ActivePlayerRing` | 自機(`target`)に追従するカラーリング。`SetActivePlayer(index)` で playerColors[index] に着色 |
| `PlayerSwitchAnnouncer` | 切替時 UI セリフ「X番さん出番ですッ！」をスケールポップ + フェードで表示 |

### 4.5 ボス系

| Class | 責務 |
|---|---|
| `BossDanmakuPattern` | リング状弾を `fireInterval` 秒ごとに発射、毎回 `ringAngleStepDeg` 度回転 |
| `BossMovementPattern` | 開始位置中心の矩形範囲内でランダム目標を選び SmoothStep で移動 |
| `EnemyOnFieldController` | スプライト assignment ヘルパー（`SetUpSprite`） |

### 4.6 弾系

| Class | 責務 |
|---|---|
| `Bullet` | velocity に従って position 更新、衝突半径 `radius` |
| `DanmakuManager` | 弾の List を管理、`fieldBounds` 外を破棄、`PlayerOnFieldHittable` と距離衝突判定、`SpawnBullet`/`ClearAllBullets` |

### 4.7 HP / UI

| Class | 責務 |
|---|---|
| `SharedHpController` | `currentHp/maxHp`、`TakeDamage`、`OnChanged`/`OnDepleted` |
| `SharedHpGaugeUI` | 画面下中央に緑色の HP バーを runtime 生成、`SetHp` で更新 |

### 4.8 共通
- `FloatingCharacterMotion` — Visual 子オブジェクトの localPosition を sin で揺らす（呼吸演出）

---

## 5. イベントフロー

### 被弾フロー
```
DanmakuManager.Update (距離判定)
  ↓ NotifyHit
PlayerOnFieldHittable (無敵チェック)
  ↓ Hit event
DodgePhase.HandlePlayerHit
  ↓ TakeDamage(1)
SharedHpController
  ├─ OnChanged → SharedHpGaugeUI.SetHp
  └─ OnDepleted → DodgePhase.HandleHpDepleted
                    ↓
                  EditorApplication.isPlaying = false
                  (or Application.Quit())
```

### ターンフロー
```
PlayerTurnManager (timer)
  ├─ ActivePlayerChanged → DodgePhase.HandleActivePlayerChanged
  │                          ├─ ActivePlayerRing.SetActivePlayer
  │                          └─ PlayerSwitchAnnouncer.Announce
  └─ RoundCompleted → DodgePhase.HandleRoundCompleted
                       ↓
                     EndDodge → EndDodgeSequence
```

### 入力ゲーティング
```
DebugMultiPlayerInput.ReadMovement
  ↓ digit_X が押されている?
  ↓ X == turnSource.CurrentActivePlayer?
  ↓ Yes → WASD+矢印を返す
  ↓ No → Vector2.zero
PlayerOnFieldController (Update)
  ↓ 移動 + bounds クランプ
```

---

## 6. デバッグ操作

| キー | 動作 |
|---|---|
| `1 + WASD/矢印` | P1 として自機を操作（P1 のターン中のみ有効） |
| `2` 〜 `4` | 同上、各プレイヤー |
| `Enter` | フェーズを次に進める（BattleStateMachine） |

数字キー解放中・ターン外の数字を押している間は移動入力ゼロ。

---

## 7. Inspector チューニングポイント（DodgePhase 主要のみ）

| カテゴリ | フィールド | 既定値 |
|---|---|---|
| 弾幕フィールド | `enemyOnFieldSpawnPosition` | (0, 256, 0) |
| | `enemyOnFieldEntryHeight` | 600 |
| | `enemyOnFieldAppearDuration` | 0.6 |
| | `enemyReturnFromAboveHeight` | 600 |
| 自機 | `playerOnFieldPrefab` | PlayerOnField.prefab |
| | `playerOnFieldBottomMargin` | 100 |
| プレイヤー | `playerColors[4]` | 赤/青/緑/黄 |
| | `playerTurnDuration` | 4.0 |
| | `playerCallouts[4]` | 「X番さん…」 |
| HP | `sharedMaxHp` | 5 |
| アニメーション | 各 `Duration` 系 | 0.3〜0.6 |

ボス挙動（弾速・間隔・移動範囲など）は **runtime に attach される `BossDanmakuPattern` / `BossMovementPattern` の Inspector** で調整。

---

## 8. ランタイム生成 vs シーン配置の方針

| パターン | 採用箇所 | 理由 |
|---|---|---|
| **Prefab を Instantiate** | EnemyOnField, PlayerOnField, BulletOnField | デザイン編集を Editor でしたい |
| **シーンに永続配置** | DanmakuManager, BattleStateMachine, Field, Player, Enemies | 同一インスタンスを使い回す |
| **runtime new GameObject** | SharedHp, HpGauge, ActivePlayerRing, TurnMgr, Announcer, BossDanmakuPattern, BossMovementPattern | フェーズ専用で破棄も自前管理 |

---

## 9. 既知の設計判断 / 制約

- **`== null` を維持**: Unity Object の特殊 null 挙動を保つ。`??=` の Hint は無視。
- **Hint (collection initializer 等) は無視**: 慣習的なリテラル形式を維持。
- **被弾は手動距離判定**: Physics2D を使わず `DanmakuManager.Update` でループ判定。弾数が膨大になったら見直し。
- **コルーチン中の event 発火**: イベントハンドラが list を破壊する可能性に対し `DanmakuManager.Update` はスナップショット反復で対応。
- **Game Over は仮実装**: 現状 `EditorApplication.isPlaying = false` / `Application.Quit()`。本実装でゲームオーバー画面に置換予定。

---

## 10. ファイル一覧

```
Assets/Src/
├── BattleStateMachine.cs
├── IBattlePhase.cs
├── CardSelectPhase.cs
├── ActionCommandPhase.cs
├── DodgePhase.cs              ← オーケストレーター
├── DodgePhaseUtils.cs
├── DanmakuManager.cs
├── Bullet.cs
├── BossDanmakuPattern.cs
├── BossMovementPattern.cs
├── EnemyOnFieldController.cs
├── EnemyOnFieldSpawner.cs     ← 未使用（旧実装）
├── IPlayerOnFieldInput.cs
├── KeyboardPlayerOnFieldInput.cs
├── DebugMultiPlayerInput.cs
├── PlayerOnFieldController.cs
├── PlayerOnFieldHittable.cs
├── PlayerTurnManager.cs
├── PlayerSwitchAnnouncer.cs
├── ActivePlayerRing.cs
├── SharedHpController.cs
├── SharedHpGaugeUI.cs
├── FloatingCharacterMotion.cs
└── PlayerUICircle.cs          ← 未使用（旧 UI 実装）
```
