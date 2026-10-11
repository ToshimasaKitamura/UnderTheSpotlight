# 弾幕システム設計（弾幕パターン / 弾種 / 難易度パラメータ）

弾幕（danmaku）の**正典**。弾の生成・弾種・難易度パラメータの定義とコード対応をここに集約する。
関連: 回避フェーズ全体は [DodgePhase_Design.md](DodgePhase_Design.md)、部位破壊による弱化は [PartDestruction_Design.md](PartDestruction_Design.md)。

## 実装の対応（コード ↔ 仕様）

| 要素 | 実装 |
|---|---|
| 弾の実体 | [`Bullet`](../../Assets/Src/Utils/Bullet.cs)（`velocity`/`radius`/`kind`/`healAmount`） |
| 弾の生成・衝突・回復弾処理 | [`DanmakuManager`](../../Assets/Src/Utils/DanmakuManager.cs) |
| ボスの弾幕パターン | [`BossDanmakuPattern`](../../Assets/Src/Enemy/BossDanmakuPattern.cs) |
| パターン起動 | [`DodgeEnemyManager`](../../Assets/Src/Dodge/DodgeEnemyManager.cs) |
| ボス別の弾幕数値 | [`EnemyData`](../../Assets/Src/Enemy/EnemyData.cs) の `danmaku*` フィールド |
| 進行ランプ | [`GameTuning`](../../Assets/Src/Utils/GameTuning.cs) の `danmakuSpeedRampPerBoss` / `danmakuDensityRampPerBoss` |

## 難易度パラメータ（[弾数] / [弾速]）

難易度は**ボス単位**で `EnemyData` に持たせる。パターン(`BossDanmakuPattern`)の Inspector 値は既定値で、
`DodgeEnemyManager` が弾幕開始時に `Configure(EnemyData, …)` で上書きする。

| EnemyData フィールド | 意味 | 難易度への効き |
|---|---|---|
| `danmakuBulletsPerRing` | 1リングの弾数 | 多いほど難しい |
| `danmakuBulletSpeed` | 弾速(ワールド単位/秒) | 速いほど難しい |
| `danmakuFireInterval` | 発射間隔(秒) | 短いほど難しい |
| `danmakuRingAngleStepDeg` | 発射ごとの角度オフセット(度) | 回転の速さ/見た目 |
| `danmakuBulletKind` | 撃つ弾の種別(下表) | 用途・脅威の質 |
| `danmakuPattern` | 弾幕パターン種別(下表) | 攻撃の形(全方位/狙い/複合) |
| `danmakuHealEveryNShots` | この発数ごとに1発を回復弾(Heal)にする(0で無効) | 自己回復ギミックの強さ |

### 弾幕パターン種別（DanmakuPatternType）

| 種別 | 挙動 |
|---|---|
| `Ring` | 全方位リング。発射ごとに `danmakuRingAngleStepDeg` ずつ回転 |
| `AimedFan` | 自機を狙う扇状の連射(`fanSpreadDeg` 幅)。ナイフ投げ的 |
| `Mixed` | Ring と AimedFan を4発ごとに交互に切替(複合) |

### 進行ランプ（連戦が進むほど強く）

1ボス＝1回避フェーズのため、ランプは**連戦のボスindex**（`RunProgress.currentBossIndex`）で段階化する。

- 実効弾速 = `danmakuBulletSpeed × (1 + danmakuSpeedRampPerBoss × bossIndex)`
- 実効弾数 = `round(danmakuBulletsPerRing × (1 + danmakuDensityRampPerBoss × bossIndex))`

その上に、部位破壊による弱化（[PartDestruction_Design.md](PartDestruction_Design.md)）が重なる。

## 弾種（BulletKind）

基本は白黒。攻撃の用途によって見た目と挙動を変える（[`BulletKind`](../../Assets/Src/Utils/Bullet.cs)）。

| 種別 | 色 | 挙動 | 用途 |
|---|---|---|---|
| `Normal` | 白 | 通常弾 | 基本の弾幕 |
| `Fast` | 淡い青 | 速弾（クナイ的） | 鋭い狙い撃ち。速さで攻める |
| `Heal` | 緑(Success) | **自機に命中するとボスが自己回復**（自機ダメージ無し） | ボスの自己回復ギミック。プレイヤーは避けるべき弾 |

- 回復弾の効果は `DanmakuManager` が `EnemyRuntimeState.HealAny(amount)` を呼んで実現する
  （未破壊かつ最大HP未満の部位を1つ回復）。回復量は `Bullet.healAmount`。
- 色は弾生成時に `DanmakuManager` が弾スプライトへ適用する。

## ボス別の割り当て（World1 一巡）

各ボスのコンセプトと数値の方向性は [タスク実行計画 §7](../タスク実行リスト/20261003_実行計画.md) を参照（A<B<C で弾数・弾速・弾種を段階導入）。
