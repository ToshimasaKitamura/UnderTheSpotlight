# 部位破壊システム 設計仕様書

戦闘中、敵の特定部位を狙って攻撃 → 部位を破壊することで敵の行動を弱体化させる仕組み。

> **⚠ 現行(仮)の挙動（2026-08-04）**: 当初設計の「攻撃カードかつ攻撃範囲内の部位のみ選択」から簡略化し、
> **カード選択の後に、どのカードでも部位選択を行う**（生存する全部位が候補。攻撃範囲による絞り込みなし）。
> さらに **非攻撃カード(回復/防御/バフ等)もデフォルトで選択部位へ攻撃**する（基礎威力 `ActionCommandPhase.nonAttackDefaultPower`）。
> 攻撃カードはカード威力で攻撃。非攻撃カードは加えて本来の効果も処理する。実装: `PartSelectPhase` / `ActionCommandPhase.HandlePlayerAttack`。

---

## 1. ユーザー提示仕様

- プレイヤーが攻撃する際、相手の **部位を選択** することができる
- 部位を破壊すると、敵の挙動が変化:
  - 攻撃が弱まる
  - 避けやすくなる
  - 回復などを阻止する
- この仕様の追加により、プレイヤーの攻撃に **「攻撃範囲」** という概念が追加される
- どの部位を破壊すると良いかは、**敵側がセリフや動きで教えてくれる**
  - 例: 「スーパージャンプをおみまいしてやるぞ!!」 → 「足を狙うと良い」と誘導

---

## 2. データモデル(提案)

### `EnemyPart` (ScriptableObject)
| フィールド | 意味 |
|---|---|
| id | 部位識別子(例: `hamster_head`) |
| displayName | 表示名(例: 「頭」) |
| maxHp | 部位のHP(破壊までに要するダメージ) |
| destructionEffect | 破壊時に発動する効果種別(enum) |
| weakHintCue | この部位を狙わせるためのヒント(セリフテンプレ/動き種別) |

### `EnemyData` (ScriptableObject)
| フィールド | 意味 |
|---|---|
| id, displayName | 敵識別子・名前 |
| parts | List<EnemyPart>(複数部位) |
| nextHintPart | 直近で「狙え」と示唆する部位(動的に変わる) |

### `PartDestructionEffect` (enum)
- WeakenNextAttack — 次の攻撃を弱化
- PreventHeal — 回復不可
- ReducePatternDensity — 弾幕を薄くする
- ReducePatternSpeed — 弾幕の速度を下げる
- DisableMovement — 移動を止める
- (拡張可)

### `AttackRange` (CardData 拡張)
- カードに `attackRange` フィールド追加(例: 単発/全体/上半身/下半身)
- 部位選択 UI でカードの攻撃範囲外の部位はグレーアウト

---

## 3. フロー(提案)

```
CardSelect で攻撃カードを選ぶ
   ↓
(新)部位選択フェーズ または ActionCommand 直前に部位選択 UI
   ↓ 敵のヒント(セリフ/動き)を見て部位を選ぶ
ActionCommand(ミニゲーム成功で命中)
   ↓ 命中なら選択部位の HP を減らす
   ↓ 部位 HP=0 で破壊 → PartDestructionEffect が発動
Dodge(弾幕)
   ↓ 破壊された部位の効果が反映(密度減/速度減/特定パターン不発)
次ターン
```

---

## 4. UI(提案)

- 敵アイコン上に **部位ヒットボックス** を重ねた選択 UI(画像例参照)
- カード選択直後 or ActionCommand 開始前に表示
- 部位はマーカー(□/△/▽等)で識別、現在のカード `attackRange` で狙えるものだけクリック可
- 敵のセリフ吹き出しでヒント(`weakHintCue` から生成)
- 破壊済み部位は赤×で表示

---

## 5. CSV / GameTuning 連動

- `enemy_parts.csv` — 敵ごとの部位定義
  ```csv
  enemyId,partId,displayName,maxHp,effect,hintCue
  hamster,head,頭,3,WeakenNextAttack,「頭を…守らなきゃ…！」
  hamster,leg,足,2,DisableMovement,「スーパージャンプをおみまいしてやるぞ!!」
  ```
- カードに attackRange 列を追加(cards.csv 拡張)

---

## 6. 既存システムとの接続

- **CardData**: `attackRange` フィールド追加(列挙: Single/Upper/Lower/All)
- **ActionCommandPhase**: ミニゲーム成功時、選択部位の HP を減らす処理を追加
- **DodgePhase**: 破壊済み部位の効果を `BossDanmakuPattern` などに渡し、密度/速度/種類を調整
- **EnemyOnFieldController**: 部位ヒットボックス UI を持つ
- **新規 `PartSelectView`**: 部位選択 UI(画像例の形式)

---

## 7. MVP 範囲(段階分け)

**Step A — データ模型のみ(最小) — ✅ 実装済み**
- ✅ `CardAttackRange` / `PartDestructionEffect` / `BodyZone` enum を `CardEnums.cs` に追加
- ✅ `EnemyData` (ScriptableObject) + 内包 `[Serializable] EnemyPart`(`Assets/Src/EnemyData.cs`)
- ✅ `CardData.attackRange` フィールド追加 + 既存カード6種に値を設定
- ✅ `cards.csv` に `attackRange` 列追加(数値0-3 or enum名どちらでも OK)
- ✅ `enemy_parts.csv` 新設 — 1行=1部位の表形式
- ✅ `CsvImporter` 拡張 — `ReimportEnemies` / `ExportEnemies` メニュー追加
- ✅ サンプル: `Assets/Data/Enemies/hamster.asset`(頭/腕/足の3部位)
- 🔒 戦闘ロジックへの適用は未実装(Step B 範囲)

**Step B — 部位選択 + 破壊反映(中) — ✅ 骨格実装済み(UI v2 仕様準拠)**

UI 仕様(ユーザー提示の図):
- 敵スプライトが画面中央上に大きく表示される
- 敵に **赤い矩形のヒットボックス** が部位ごとに重なる(EnemyPart.hitboxCenter/Size 正規化座標)
- 選択中の矩形は **黄+太枠+脈動** でハイライト
- 敵の右側に **マンガ風ギザギザ吹き出し**(SpeechBubble + BurstGraphic) で hintCue を表示
- 下部に部位チップ一覧(名前+残HP) + 緑のプログレスバー + タイマー秒数
- 入力: ↑↓ or ←→ で選択、Enter/Space/Z で確定、タイマー切れで自動確定

ヒントは「急所として相手の攻撃前のセリフで分かる」仕様:
- **タイミング: 選択 → セリフ → 攻撃に移る**
  - PartSelect 中に吹き出しは出さない(プレイヤーが選んでいる最中に敵が反応するのはおかしい)
  - PartSelect 開始時に `EnemyRuntimeState.PickNextTelegraph()` で今ターンの予告部位だけ決めておく(UI には出さない)
  - ActionCommand を経て **Dodge 開始の直前**, 敵が登場して動き出す前の 2.5 秒間に吹き出しを大きく出す
  - 吹き出し消失と同時に弾幕パターン開始 = 「セリフ → 攻撃」の流れ
- 学習サイクル: 初回ターンは予告を聞いて部位の意味を覚える → 次のターンの PartSelect でその知識を活用して狙う部位を決める
- セリフ自体: 予告部位の `hintCue` をそのまま表示(連続ターンで同じ部位は避ける)
- 予告された部位を破壊できていれば、対応する `destructionEffect` で Dodge 攻撃が弱化/無効化(例: legs 予告 → 足破壊で DisableMovement → Dodge でボス静止)
- Step C: 攻撃パターン複数化、予告と攻撃の対応強化、吹き出しを敵頭上にトラッキング配置(現状は画面上端中央)

PartSelect 画面の作り(改訂):
- **吹き出しなし**(セリフは Dodge 直前に集約)
- **新しい敵パネル/プレースホルダは一切作らない**。シーンに最初から配置されている `Assets/Prefabs/Enemy.prefab` の GameObject(子 "Visual" に SpriteRenderer + FloatingCharacterMotion)を直接使う
- `PartSelectPhase.enemyTransform` (Inspector 指定可、未指定なら `GameObject.Find("Enemy")` で自動取得) → `Visual` 子を参照
- `PartSelectView.BuildHitboxesOnEnemy()` が Visual の子に **WorldSpace Canvas** を貼り、その上に赤い矩形 UI(Image + Outline)をヒットボックスとして配置
  - Canvas の `sizeDelta = sprite.bounds.size` (sprite ローカル単位)、`localScale = 1` → Visual の transform scale を継承してそのまま敵と同じワールドサイズに
  - 浮遊モーションにも自然追従する
- overlay Canvas には ヘッダ / 部位チップ / タイマー / 緑プログレスバー / 操作ヒント のみ(敵を隠さない位置)

- ✅ `EnemyRuntimeState` (シーン常駐 MonoBehaviour) — 部位HP+破壊フラグ管理。`OnPartDestroyed`/`OnPartHpChanged` イベント、`HasActiveEffect(effect)`、`EnumerateSelectableParts(range)` API。
- ✅ `PartSelectPhase` (IBattlePhase) — CardSelect と ActionCommand の間に挿入。攻撃カテゴリかつ範囲が All 以外の場合のみホットシートで部位選択。
- ✅ `PartSelectView` — Legacy Text の縦リスト UI。部位名/残HP/[上半身|下半身]/hintCue を表示。↑↓で選択 / Enter で確定。
- ✅ `BattleStateMachine.State` に `PartSelect` を追加し CardSelect→PartSelect→ActionCommand の遷移を確立。
- ✅ `ActionCommandPhase.ApplyPartDamage()` — 各プレイヤーの成功数を `perPlayerSuccess[]` で追跡し、攻撃カードの選択部位(または All の場合は全部位)に `card.EffectivePower` ぶんダメージを与える。
- ✅ `BossDanmakuPattern` — `WeakenNextAttack` 検出時に弾速 ×0.7・弾数 ×0.6 を適用(`ReducePatternSpeed`/`Density` にも反応)。
- ✅ `DodgePhase` — `DisableMovement` 検出時に `BossMovementPattern.StartPattern()` をスキップ。
- 🔒 PreventHeal は未実装(敵の自己回復スキル自体が無いため、Step C で先に回復行動を作る)。
- 🔒 敵セリフ吹き出し/動きヒント演出は未実装(hintCue は PartSelect UI でのみ表示中)。

**Step C — フル(大)**
- 全 destruction effect 実装
- 敵ヒント(セリフ/動き)演出
- AttackRange による UI のグレーアウト・カテゴリ別ミニゲーム連動

---

## Step A 実装メモ(参照用)

### enum 定義 (Assets/Src/CardEnums.cs)

| enum | 値 |
|---|---|
| `CardAttackRange` | `Single=0` / `Upper=1` / `Lower=2` / `All=3` |
| `BodyZone` | `Upper=0` / `Lower=1` |
| `PartDestructionEffect` | `None=0` / `WeakenNextAttack=1` / `PreventHeal=2` / `ReducePatternDensity=3` / `ReducePatternSpeed=4` / `DisableMovement=5` |

### CSV スキーマ

**cards.csv 拡張列**
```
id,displayName,description,category,tier,
baseDifficultyModifier,basePower,difficultyPerUpgrade,powerPerUpgrade,
effectType,attackRange,cardColor
```
- `attackRange` は数値(0-3)でも enum 名("Single"/"Upper"/"Lower"/"All")でも可
- 既定: 攻撃カテゴリは `Single`、それ以外は `All` (CSV 未指定時)

**enemy_parts.csv(新設)**
```
enemyId,enemyDisplayName,partId,partDisplayName,zone,maxHp,effect,hintCue,
hitboxCenterX,hitboxCenterY,hitboxSizeX,hitboxSizeY
```
- 1行 = 1部位。同じ `enemyId` の行が複数並ぶことで複数部位を表現
- `zone` は `Upper`/`Lower` のいずれか
- `effect` は `PartDestructionEffect` の enum 名 or 整数値
- `hitboxCenterX/Y` `hitboxSizeX/Y` は **敵スプライト矩形に対する正規化座標 (0..1)**
  - center=(0.5, 0.5) で中央、(0,0) が左下、(1,1) が右上
  - size=(0.4, 0.3) なら矩形幅は敵パネルの40%、高さ30%
- インポート時に `Assets/Data/Enemies/{enemyId}.asset` を生成/更新

### 既存システムへの影響(Step A 時点)

- CardData.attackRange は **データのみ**。CardHandView / CardSelectPhase / ActionCommandPhase は未参照。
- EnemyData は **どこからも参照されていない**。Step B で PartSelectView を作る際にロードして使う想定。
- DodgePhase の BossDanmakuPattern も destructionEffect を参照しない(Step B で接続)。
- ⚠ つまり Step A 単体ではプレイ挙動は変わらない。CSV 編集 → Reimport で SO が更新されることだけ確認できる。

---

## Step B シーン配線 手順(ユーザー操作)

新規コンポーネントをシーンに追加する必要がある。`URP2DSceneTemplate.unity` を Unity で開いて以下を実施：

1. **BattleStateMachine GameObject** を選択(`Add Component` で追加するもの)
   - `EnemyRuntimeState` を Add Component
     - `Enemy Data` フィールドに `Assets/Data/Enemies/hamster.asset` をドラッグ
   - `PartSelectPhase` を Add Component
     - `State Machine` / `Card Select Phase` / `Enemy State` フィールドは空でも Awake で自動探索
     - `Tuning` フィールドに `Assets/Data/GameTuning.asset` をドラッグ(任意)
2. **BattleStateMachine コンポーネント** の Inspector で
   - `Part Select Phase` フィールドに 1 で追加した `PartSelectPhase` をドラッグ
3. **ActionCommandPhase コンポーネント** の Inspector で
   - `Part Select Phase` フィールドに同じものをドラッグ
   - `Enemy State` フィールドに同じ GameObject の `EnemyRuntimeState` をドラッグ
4. Save Scene

配線が抜けていても `Awake` で `FindObjectOfType` するため動くが、Inspector に明示するほうが安全。

---

## Step B 動作確認手順

1. Play
2. CardSelect で全員カードを選択(P1〜P4)
3. PartSelect 画面が出現:
   - 攻撃カード(AttackCard)を選んだプレイヤーは部位選択 UI が表示される
   - スポットライト/ガード/ヒール/バフ/フィナーレ(=範囲 All)はスキップされる
4. ↑↓で部位を選んで Enter で確定
5. ActionCommand のミニゲームが始まる
6. ミニゲーム成功すると、Console に `[ActionCommandPhase] P1 → head に 3 dmg` のようなログが出る
7. 部位HPが 0 になると `[EnemyRuntimeState] 部位破壊: 頭 (head) effect=WeakenNextAttack` ログ
8. Dodge フェーズ:
   - 頭破壊 → 弾速・弾数が下がる(目視で弾幕が薄く・遅くなる)
   - 足破壊 → ボスが動かない(画面中央で静止)
9. Dodge 終了 → CardSelect に戻る。**部位の破壊状態は持続**するので、次ターンの PartSelect では破壊済み部位がリストから消える

---

## 8. 未確定 / 検討中

- 部位選択は **誰が** 行うか? (現在のカード選択者 / 全員投票 / アクティブプレイヤー)
- ヒントは部位選択 UI に表示? Dodge 中?
- 攻撃範囲は 4 種程度? もっと細分化?
