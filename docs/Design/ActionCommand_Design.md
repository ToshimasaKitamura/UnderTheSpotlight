# ActionCommand(ミニゲーム)フェーズ 設計仕様書

戦闘フロー: **CardSelect → PartSelect → ActionCommand → Dodge(弾幕回避)** の中間フェーズ。
**全員参加型のグループミニゲーム** を1回プレイし、正しい入力をした瞬間にそのプレイヤーの攻撃が
即座にエンタメ演出+部位ダメージとして発火する。
バイブコーディング時はこのドキュメントを参照。数値はすべて Inspector/アセット調整前提。

---

## ⚠ 設計刷新メモ (現行版)

旧設計はホットシート式(各ラウンド 1人だけ操作)だったが、ユーザー仕様により
「**全員で1つのミニゲームを同時にプレイ**し、**正しいボタンを押した瞬間**に
そのプレイヤーの攻撃が敵に入る(エフェクト付き)」へ刷新済み。

- 旧: 1ラウンド = 1プレイヤー × 4ラウンド分繰り返し → 最後にダメージ集計
- 新: 1ミニゲーム = 全4プレイヤー同時 → **押した瞬間に**即時ダメージ+演出
- 旧の Mash/Timing/Sequence/Hold は単独プレイ実装としてコードベースに残るが、
  `RunSequence` からは呼ばれない(将来「グループ式の Timing」等を追加する際の参考)。
- `MicroGameContext.OnPlayerAttack(playerIndex)` コールバックで個別命中を通知する仕組みを導入。
- 現在のグループ式実装は **リレー式 4種**(下の「グループ型アクションコマンド一覧」)。毎回ランダムに1つ選ばれる。
  旧 `GroupButtonMicroGame`(1人1キー: P1=1, P2=2, P3=3, P4=4)はコードに残るが選出対象外。

---

## 1. ユーザー提示仕様

- カード選択後 → ActionCommand → 終わったら弾幕回避フェーズ
- **全員が決定ボタンを押すとミニゲーム開始**
- ミニゲームの選出は**ランダム**。選ばれた物の名前を「MiniGameStart」表示に出す
- アクションコマンドは**どんどん難しくなる**
- カード効果で上昇する要素: **上り幅(ramp) / ボタン数 / 担当範囲 / 視覚妨害 / 速度** など
- カード効果には**個人に降りかかるデメリット**(例「青の範囲の速度が上がる」)と**全体デメリット**がある
- デメリット多いのに上手くできたら**特別な称賛演出**(やりたい/未確定)
- 通常は全員で攻撃だが、**選択キャラ/カード効果で回復等に変化**することがある

---

## 2. 提案アーキテクチャ(責務分離)

| クラス | 種別 | 責務 |
|---|---|---|
| `ActionCommandPhase` | MonoBehaviour(IBattlePhase) | 全体統制: 入力受付ゲート→ランダム選出→ラウンド進行(難化)→集計→Dodge遷移 |
| `IMicroGame` | interface | `Begin(ctx)` / `Tick()->bool(done)` / `Success` 。1ミニゲーム |
| `MicroGameContext` | class | 難易度パラメータ(speed/buttonCount/range/interference/ramp)+ playerIndex + 乱数 |
| `MicroGameRegistry` | クラス/SO | 利用可能ミニゲーム一覧 + 表示名。ランダム選出元 |
| `ActionCommandModifiers` | class | CardSelect.Selections を集計し各ノブ(速度/数/範囲/妨害/ramp)を算出。個人別/全体別 |
| `MiniGameStartView` | MonoBehaviour(runtime UI) | 「MiniGameStart」+ 選ばれたゲーム名 + 各プレイヤーの決定状況、全員確定で開始 |
| `MicroGameResultTally` | class | 成功数/デメリット数を集計。Dodge の SpecialSuccessScaling・称賛演出に供給 |

### データの受け渡し
- `CardSelectPhase.Selections[playerIndex]` (CardInstance) を ActionCommandPhase が参照
- `CardInstance.EffectiveDifficulty` と `CardEffectType` から `ActionCommandModifiers` を構築
- 成功集計は次フェーズ(Dodge)へ受け渡し(`SpecialSuccessScaling` 等の効果に接続)

---

## 3. フロー

```
Enter
 └ CardSelect.Selections 取得 → ActionCommandModifiers 構築
 └ MiniGameRegistry からランダム選出 → MiniGameStartView に名前表示
 └ 全員が決定ボタン → ラウンド開始
ラウンド i (i=0,1,2,...):
 └ MicroGameContext 構築(基本難度 + ramp*i + カード効果ノブ)
 └ 選ばれた IMicroGame.Begin → Tick ループ(数秒) → Success 判定
 └ Tally 更新 / 次ラウンドは難化
終了条件(ラウンド数 or 失敗 等, 要確認) → Tally を保持 → Dodge へ ChangeState
```

### カード効果ノブ(ActionCommandModifiers が算出)
| ノブ | 意味 |
|---|---|
| speedMul | 進行/対象の速度倍率 |
| buttonCount | 入力ボタン数 |
| rangeScale | 担当範囲の広さ |
| interference | 視覚妨害の強さ(0..1) |
| rampMul | ラウンド毎難化の上り幅倍率 |
| scope | Personal(その個人のみ) / Global(全員) |

`CardEffectType` ごとに上記へのマッピングを持つ(初期は数値テーブル, Inspector 調整)。

---

## 4. ミニゲーム(WarioWare 的・即終わる)

候補(MVP は一部):
- **連打(Mash)**: 制限時間内にボタン連打でゲージを満たす
- **タイミング(Timing)**: 動くマーカーが的に重なった瞬間に押す
- **順番入力(Sequence)**: 表示された順にボタンを押す(buttonCount で長さ)
- (将来) 回避/なぞり/選択 等

カテゴリによる変化: 攻撃カード→攻撃モーション、回復カード→回復モーション等(演出差し替え。MVPは結果共通+表示色のみ差別化)。

---

## 4b. グループ型アクションコマンド一覧(現行・ランダム選出)

共通の進行は `GroupRelayMicroGame`(基底クラス)が持つ。
1本のバーを参加人数ぶんの領域に分け、**P1→P2→…の順に手番プレイヤーが自分の領域で操作**する。
手番で成功(HIT)したら即 `OnPlayerAttack` → ダメージ演出。失敗/時間切れは不発(MISS)。
光が次の領域へ移って次の手番になる。1人あたりの制限時間は `timeLimit`(下限3秒、連打のみ下限4秒)。

| # | 名前 | クラス | 操作 | 成功条件 | 難易度ノブの効き方 |
|---|---|---|---|---|---|
| 0 | れんだ！ | `GroupMashMicroGame` | SPACE 連打 | 領域ゲージを満タンにする | speedMul: 1押しの増加が減り減衰が増える |
| 1 | タイミング！ | `GroupTimingMicroGame` | SPACE 1回 | 往復するマーカーを光る「ねらい枠」で止める | speedMul: マーカー速度↑・枠が狭く / rangeScale: 枠の広さ / interference: マーカーが点滅して見えにくい |
| 2 | ためて はなせ！ | `GroupHoldMicroGame` | SPACE 長押し→離す | 離した時のゲージが「ねらい帯」の中。満タンまで溜めると破裂で不発 | speedMul: 溜まる速さ / rangeScale: 帯の広さ |
| 3 | じゅんばんに！ | `GroupSequenceMicroGame` | 矢印キー or WASD | 表示された矢印を左から順に全部入力。1回でも間違えたら不発 | buttonCount: 矢印の数(2〜6) / interference≥0.5: 2つ先以降が「？」で隠れる |

- 選出: `MicroGameRegistry.CreateGroupDefault()` からランダム。**増やすときは `GroupRelayMicroGame` を継承し、ここに1行登録する**(派生は `BuildZone` と `TickTurn` を書くだけ)。
- 動作確認: `ActionCommandPhase.debugForceGroupGame` に上表の番号を入れると固定。-1 でランダム。
- タイミング/ためて/じゅんばんに は手番終了時に領域の上へ「HIT!/MISS」を表示する(れんだは塗りで分かるため出さない)。
- 押しっぱなし対策: ためて はなせ！ は手番中に新しく押した時から溜め始める(前の手番から押しっぱなしでも溜まらない)。

## 5. 確定事項 / 実装状況

- ミニゲーム: 拡張可能フレーム + 初期4種実装(連打/タイミング/順番入力/ホールド)。`MicroGameRegistry.Register` 追加で増種
- 決定方式: **ホットシートで1人ずつ順に決定**(`MiniGameStartView`)
- 効果深度: **難化ramp + ノブ雛形**(`ActionCommandModifiers`)。CardSelect 難度合計で初期バイアス、ラウンド毎難化。speed/buttons/range/interference のノブは枠実装、係数は `ActionCommandPhase` Inspector
- ラウンド終了: **固定ラウンド数**(`roundsPerActionCommand`、既定3。Inspector調整)。各ラウンド毎にゲーム再抽選で多種が出る
- 集計: `SuccessCount`/`RoundsPlayed` を公開(Dodge の SpecialSuccessScaling・称賛演出へ将来接続)

### 後段(未実装・TODO)
- カード `CardEffectType` ごとの個人/全体デメリットの具体マッピング(現状は難度合計の総和バイアスのみ)
- デメリット多で好成績時の**称賛演出**
- 攻撃/回復モーション差し替え(カテゴリ別)
- 視覚妨害(interference)のミニゲーム別演出強化

## 6b. 実装ファイル

```
AcUi.cs                共通 UI(Legacy Text/Image, OS和文フォント)
IMicroGame.cs          IMicroGame + MicroGameContext(難易度ノブ)
GroupRelayMicroGame.cs     グループ型(リレー式)の共通基底: 領域分割・交代の光・タイマー・HIT/MISS
GroupMashMicroGame.cs      れんだ！
GroupTimingMicroGame.cs    タイミング！
GroupHoldMicroGame.cs      ためて はなせ！
GroupSequenceMicroGame.cs  じゅんばんに！
MashMicroGame.cs       連打
TimingMicroGame.cs     タイミング
SequenceMicroGame.cs   順番入力
HoldMicroGame.cs       ホールド
MicroGameRegistry.cs   登録簿(ランダム選出)。グループ型は CreateGroupDefault()
ActionCommandModifiers.cs  CardSelect→ノブ算出(雛形)
MiniGameStartView.cs   MiniGameStart + ホットシート決定
ActionCommandPhase.cs  統制(IBattlePhase, BlocksDebugAdvance=true)
```

---

## 6. 既存資産との接続

- `BattleStateMachine` の State.ActionCommand は既存(現状スタブ)。`BlocksDebugAdvance` は実装中 false→要再検討
- `CardSelectPhase.Selections` を公開済(`IReadOnlyList<CardInstance>`)
- 入力は新 Input System(`Keyboard.current`)。デバッグ多人数は CardSelect と同様ホットシート方針
