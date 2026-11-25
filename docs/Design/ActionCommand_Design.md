# ActionCommand(ミニゲーム)フェーズ 設計仕様書

戦闘フロー: **CardSelect → PartSelect → ActionCommand → Dodge(弾幕回避)** の中間フェーズ。
**全員参加のリレー式ミニゲーム**を1回プレイし、手番のプレイヤーが成功(HIT)した瞬間に、そのプレイヤーの攻撃が
エンタメ演出+部位ダメージとして即座に発火する。数値は GameTuning(CSV)で調整する([CsvWorkflow.md](CsvWorkflow.md))。

---

## 1. ユーザー提示仕様

- カード選択後 → ActionCommand → 終わったら弾幕回避フェーズ
- ミニゲームは**順番に**出る(2026-11 ユーザー指定で変更。旧仕様はランダム)
- **P1→P2→… の順に1人ずつ**操作する(4人でも順番に回る)
- アクションコマンドは**どんどん難しくなる**
- カード効果で上昇する要素: **上り幅(ramp) / ボタン数 / 担当範囲 / 視覚妨害 / 速度** など
- カード効果には**個人に降りかかるデメリット**(例「青の範囲の速度が上がる」)と**全体デメリット**がある
- デメリット多いのに上手くできたら**特別な称賛演出**(やりたい/未確定)
- 通常は全員で攻撃だが、**選択キャラ/カード効果で回復等に変化**することがある

> 画面構成の経緯: 2026-11-10 に「1枚の大きなフィールドを1人ずつ使う」案(丸・三角などの図形)を実装したが、
> 2026-11-25 にオーナーの判断で**横ゲージを人数分に分ける方式**へ戻した。現行は下記 §3 の横ゲージ方式。

---

## 2. 構成(責務分離)

| クラス | 種別 | 責務 |
|---|---|---|
| `ActionCommandPhase` | MonoBehaviour(IBattlePhase) | 全体統制: 種目を順番に選ぶ → ミニゲームを1回実行 → 命中ごとにダメージ/演出 → BattleResult へ |
| `IMicroGame` | interface | `Begin(ctx)` / `Tick(dt)->bool(終了)` / `Succeeded` / `Cleanup()` |
| `MicroGameContext` | class | 難易度ノブ(timeLimit/speedMul/buttonCount/rangeScale/interference)・人数・色・乱数・命中コールバック `OnPlayerAttack` |
| `MicroGameRegistry` | class | ミニゲームの登録簿。`CreateGroupDefault()` の登録順＝巡回順 |
| `ActionCommandModifiers` | class | CardSelect の選択(難度合計)から各ノブを算出 |
| `GroupRelayMicroGame` | 抽象基底 | 横ゲージ方式の共通進行(下記 §3) |

### データの受け渡し
- `CardSelectPhase.Selections[playerIndex]`(CardInstance)を ActionCommandPhase が参照
- 部位は `PartSelectPhase.TargetPartIds[playerIndex]`。狙った部位が先に壊されていたら生存部位へ振り替える([PartDestruction_Design.md](PartDestruction_Design.md))
- 成功集計 `SuccessCount` / `PerPlayerSuccess` は Dodge 終了時の効果処理(`BattleEffectProcessor.OnDodgePhaseEnd`)へ渡す

### カード効果ノブ(ActionCommandModifiers が算出)
| ノブ | 意味 |
|---|---|
| timeLimit | 1人あたりの制限時間の基準 |
| speedMul | 進行/対象の速度倍率 |
| buttonCount | 入力ボタン数 |
| rangeScale | ねらい枠・帯などの広さ(小さいほど難しい) |
| interference | 視覚妨害の強さ(0..1) |

---

## 3. 横ゲージ方式(`GroupRelayMicroGame`)

画面中央に1本の大きなバー(940x92)を出し、**参加人数ぶんの領域**に分ける。各領域はプレイヤー色。

```
        P1  SPACE を れんだ！                      ← 操作説明(手番プレイヤーの色・大きい文字)
[ P1 の領域 | P2 の領域 | P3 の領域 | P4 の領域 ]   ← 手番の領域はプレイヤー色で脈動(背景側)
   HIT!        MISS                                ← 手番が終わると領域の上に結果(れんだ以外)
   P1          P2          P3          P4
```

- 進行: P1 の手番 → 終わったら光が次の領域へ移動(0.26秒)→ P2 の手番 → …
- 手番中に成功(HIT)したら即 `OnPlayerAttack` → ダメージ光(始点=その領域の右端)+部位ダメージ+HIT 表示(その領域の位置)
- **ボスを倒したら、その場でアクションコマンドを中断**し、残りのプレイヤーの手番は行わない(撃破演出は BattleResultPhase)
- 1人あたりの制限時間は `timeLimit`(下限3秒、れんだのみ下限4秒)。時間切れは MISS。右側にタイマー表示
- 操作説明(`PromptText`)は、バーの上に**大きな文字(44px)で黒い下地の上に**出す。手番プレイヤーの番号と色で「P1  SPACE を れんだ！」のように表示し、手番が替わると更新する(以前の IMGUI の小さいドット文字は読みにくかったため、uGUI の本文フォントに変更)

### 種類(登録順=出る順番)
| # | 名前 | クラス | 操作 | 成功条件 | 難易度ノブの効き方 |
|---|---|---|---|---|---|
| 0 | れんだ！ | `GroupMashMicroGame` | SPACE 連打 | 自分の領域のゲージを満タンにする(止めると少し減る) | speedMul: 1押しの量が減り、減る速さが増える |
| 1 | タイミング！ | `GroupTimingMicroGame` | SPACE 1回 | 往復するマーカーを光る「ねらい枠」で止める | speedMul: マーカー速度↑・枠が狭く / rangeScale: 枠の広さ / interference: マーカーが点滅 |
| 2 | ためて はなせ！ | `GroupHoldMicroGame` | SPACE 長押し→離す | 離した時のゲージが「ねらい帯」の中。満タンまで溜めると破裂で不発 | speedMul: 溜まる速さ / rangeScale: 帯の広さ |
| 3 | じゅんばんに！ | `GroupSequenceMicroGame` | 矢印キー or WASD | 表示された矢印を左から順に全部入力。1回でも違えば不発 | buttonCount: 矢印の数(2〜6) / interference≥0.5: 2つ先以降が「？」で隠れる |

- 選出: **順番に巡回**(れんだ→タイミング→ためて はなせ→じゅんばんに→れんだ…)。フェーズのたびに次へ進む。
  巡回位置は `ActionCommandPhase` が保持し、バトルシーンを読み込み直すと先頭から。
- 押しっぱなし対策: ためて はなせ！ は手番中に新しく押した時から溜め始める。手番交代の光の間は入力を受け付けない。
- 動作確認: `ActionCommandPhase.debugForceGroupGame` に上表の番号を入れると固定(-1 で巡回)。範囲外はエラー。

### 種目の増やし方
1. `GroupRelayMicroGame` を継承し、`DisplayName` / `PromptText` / `BuildZone(i, zone, col)`(領域の中身を作る)/ `TickTurn(i, dt)`(判定。`TurnResult.Hit/Miss/Continue` を返す)を実装する
2. 必要なら `OnSetup`(難易度の初期化)/ `OnTurnStart` / `OnTurnEnd` / `ComputeTurnLimit` / `ShowResultLabels` を上書きする
3. `MicroGameRegistry.CreateGroupDefault()` に1行登録する(登録順＝巡回順)

---

## 4. 実装状況 / TODO

- 実装済み: 横ゲージ方式の4種、順番に巡回、カード難度による難化バイアス、撃破時の即中断
- 未実装(TODO)
  - カード `CardEffectType` ごとの個人/全体デメリットの具体マッピング(現状は難度合計による全体バイアスのみ)
  - デメリット多で好成績時の**称賛演出**
  - 攻撃/回復モーション差し替え(カテゴリ別)
  - 視覚妨害(interference)のミニゲーム別演出強化

---

## 5. 実装ファイル(`Assets/Src/MicroGame/`)

```
ActionCommandPhase.cs      統制(IBattlePhase)。命中時のダメージ・演出、HIT 表示、BattleResult への遷移
IMicroGame.cs              IMicroGame + MicroGameContext(難易度ノブ)
MicroGameRegistry.cs       登録簿。CreateGroupDefault()(登録順=巡回順)
ActionCommandModifiers.cs  CardSelect→ノブ算出
GroupRelayMicroGame.cs     横ゲージ方式の共通基底: 領域分割・交代の光・タイマー・HIT/MISS・攻撃の発火
GroupMashMicroGame.cs      れんだ！
GroupTimingMicroGame.cs    タイミング！
GroupHoldMicroGame.cs      ためて はなせ！
GroupSequenceMicroGame.cs  じゅんばんに！
AcUi.cs / AcImgui.cs       共通 UI ヘルパー(uGUI の Image/Text 生成、タイマー枠の描画)
MicroGameIMGUIHelper.cs    タイマー表示(OnGUI)
```
