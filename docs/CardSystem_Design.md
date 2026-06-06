# カード / デッキシステム 設計仕様書

カード選択フェーズおよびデッキ機構の Unity 実装仕様。
バイブコーディング時はこのドキュメントを参照すること。
**数値はすべて ScriptableObject / Inspector で調整可能**（コードにハードコードしない）。

> 📌 メタ進行(マップ・キャラレベル・カード永続強化)はこの戦闘内システムの**上位レイヤー**として
> 別文書で管理: [StageMap_Design.md](StageMap_Design.md) / [CharacterPassive_Design.md](CharacterPassive_Design.md) /
> [CardMetaUpgrade_Design.md](CardMetaUpgrade_Design.md)。本書はあくまで「1戦闘内」のカード/デッキ仕様。

---

## 1. 概要

- デッキは DeckDefinition アセットで構成（既定サンプル = 30枚: 基本20/特殊9/必殺1）
- 一度に `drawCount` 枚（既定3）引く。アイテム等で増減可
- 山札が尽きると捨札を戻し、**強化レベル +1**（全カードの実効難度/威力が上昇）して再シャッフル
- ステージイベントでデッキ内容を増減可能（AddCard / RemoveCard / RebuildFrom）
- カードは 5カテゴリ（攻撃/防御/回復/バフ/特殊）× 3階層（基本/特殊/必殺）
- 各カードに「難度修正」と「戦闘効果(effectType)」を持つ（効果ロジックの戦闘適用は次段階）

---

## 2. クラス構成

| Class | 種別 | 責務 |
|---|---|---|
| `CardCategory` / `CardTier` / `CardEffectType` | enum | カテゴリ / 階層 / 効果種別 |
| `CardData` | ScriptableObject | カード1種の定義（基本値 + 強化増分 + 効果種別 + 表示） |
| `CardInstance` | ランタイム class | CardData 参照 + 強化レベル。`EffectiveDifficulty/EffectivePower` を提供 |
| `DeckDefinition` | ScriptableObject | デッキ構成（`List<{card, copies}>`） |
| `PlayerDeck` | プレーン C# | 1人分の山札/手札/捨札。Draw/Discard、枯渇時 Cycle+強化、ステージ API |
| `PartyDeckController` | MonoBehaviour(常駐) | プレイヤー人数分の `PlayerDeck[]`、各自のデッキ定義、`drawCount` |
| `CardSelectPhase` | MonoBehaviour(IBattlePhase) | **1画面ホットシート**: ターン順→1人ずつドロー→制限時間選択→全員完了で遷移 |
| `CardHandView` | MonoBehaviour(runtime UI) | レトロ・アーケード調。**フレームは階層別**(基本=素朴/特殊=装飾内枠/必殺=金多重枠+四隅スタッド) + スキャンライン。文字は **Legacy Text + OS動的フォント**。難度のみ表示(力/power は仕様外で非表示)。右下に横一列、見出し/タイマー、山札スタック、弧を描く配り演出、選択グロー |
| `CardSelectStage` | MonoBehaviour(runtime) | カメラを主人公側へズームイン、アクティブキャラを画面左に巨大表示、暗幕 + スポットライト + 他キャラ暗転 + 確定リアクション、終了でカメラ復帰 |

---

## 3. データモデル

### CardData（定義）
| フィールド | 意味 |
|---|---|
| id / displayName / description | 識別子・表示名・説明（効果テキスト） |
| category | Attack/Defend/Heal/Buff/Special |
| tier | Basic/Special/Ultimate（構成比に対応） |
| baseDifficultyModifier | 難度修正の基本値（+で難、-で易） |
| basePower | 威力の基本値 |
| difficultyPerUpgrade | 強化1段あたり難度増分 |
| powerPerUpgrade | 強化1段あたり威力増分 |
| effectType | 戦闘効果ロジック種別（現状データ保持のみ） |
| cardColor / icon | 表示 |

### 実効値の算出（CardInstance）
```
EffectiveDifficulty = baseDifficultyModifier + difficultyPerUpgrade * UpgradeLevel
EffectivePower      = basePower            + powerPerUpgrade      * UpgradeLevel
```
`UpgradeLevel` はデッキ1周ごとに PlayerDeck が +1 し、その PlayerDeck の全 CardInstance に再付与。

---

## 4. デッキ機構フロー(各自別デッキ)

```
PartyDeckController.Awake → BuildDecks()
  └ プレイヤー人数分の PlayerDeck を生成(各自 perPlayerDecks[i] から構築/シャッフル, seed=base+i)

CardSelectPhase.Enter
  └ ターン順決定 → ターン順に1人ずつ:
       PlayerDeck.DrawHand(drawCount)
         ├ 直前の手札を捨札へ
         ├ drawCount 枚 山札から
         └ 山札が尽きたら CycleDeck(): Lv++ / 捨札を戻す / 全カード再付与 / 再シャッフル / DeckCycled
       選択確定 → PlayerDeck.DiscardHand()
```

### PlayerDeck 公開 API（アイテム/ステージイベント用）
- `DrawHand(n)` / `DiscardHand()`
- `AddCard(CardData, copies)` — 山札にランダム挿入（現強化Lvで）
- `RemoveCard(CardData, copies)` — 山札→捨札の順で除去
- `RebuildFrom(DeckDefinition)` — デッキ総入れ替え（強化Lvリセット）
- イベント: `DeckCycled(int newLevel)`

### PartyDeckController API
- `GetDeck(playerIndex)` — 該当 PlayerDeck
- `SetDrawCount(int)` / `ModifyDrawCount(int)` — 全体ドロー枚数

---

## 5. CardSelectPhase（1画面ホットシート）↔ BattleStateMachine

- `Enter`: `playerCount` 分の選択枠と `turnOrder`(固定 or ランダム)を用意 → `CardSelectStage` 生成 → `BeginPlayerTurn`
- `BeginPlayerTurn`: そのプレイヤーの `PlayerDeck.DrawHand` → `CardSelectStage.FocusPlayer(i)`(カメラがそのキャラへズームイン/左に巨大表示, 他暗転, スポットライト) → `CardHandView` 生成(右下の山札から弧を描いて1枚ずつ配られ横一列に並ぶ) → 見出し「N番さん 選んでください」+ タイマー開始
- レイアウト: **左=巨大なアクティブキャラ(カメラフレーミング) / 右下=横並びカード+山札(スクリーン空間)**。重ならない
- `Tick`: タイマー減算→`SetTimer`、0で自動確定(カーソル位置)。`←→/AD`(=`↑↓/WS` も可)でカーソル、`Enter/Space/Z` 確定
- 確定 → `HandleCardConfirmed`: `selections[player]` 保存 → `DiscardHand` → `CardSelectStage.PlayReaction(i)`(スケールパンチ) → `AdvanceTurn`
- `Exit`/`FinishSelection`: `CardSelectStage.Restore()` で全キャラ位置/色/スケール復帰、暗幕/スポット破棄
- 全員終了 → `FinishSelection`: `AllSelected` 発火 → `ChangeState(ActionCommand)`
- 確定は `CardHandView.Confirm()` が**同期発火**。`BattleStateMachine.Update` は Tick 前後で `prevState` 比較しデバッグ Enter 二重発火を抑止
- 結果は `CardSelectPhase.Selections[playerIndex]`(次フェーズが参照)

---

## 6. シーン配置

| GameObject | 追加コンポーネント | 備考 |
|---|---|---|
| `BattleStateMachine` | `PartyDeckController` | 常駐。`perPlayerDecks`=4×StandardDeck, `playerCount=4`, `drawCount=3` |
| `CardSelectPhase`(子) | `CardSelectPhase` | `stateMachine`, `partyDeck`, `playerTransforms`(4キャラ) 配線済。`selectTimeLimit=15`, `randomizeTurnOrder=off` |

`CardHandView` は CardSelectPhase が手番ごとに runtime 生成し、手番終了/Exit で破棄。

---

## 7. アセット

```
Assets/Data/
├── StandardDeck.asset            DeckDefinition(基本20/特殊9/必殺1=30)
└── Cards/
    ├── AttackCard.asset   攻撃 基本 難+3 力3 効果:無被弾大ダメージ
    ├── DefendCard.asset   防御 基本 難+1 力2 効果:被弾誘導+軽減
    ├── HealCard.asset     回復 基本 難-1 力2 効果:即時+ギリ回避回復
    ├── BuffCard.asset     バフ 基本 難+1 力2 効果:与ダメ上昇
    ├── SpecialCard.asset  特殊 特殊 難+5 力4 効果:成功数で軽減
    └── UltimateCard.asset 特殊 必殺 難+8 力12 効果:超特大ダメージ
```

新カードは Create > UnderTheSpotlight > CardData、新デッキは同 > DeckDefinition で作成。

---

## 7.5 戦闘開始〜選択フロー

- 2D。カード選択中のみカメラが主人公側へズームイン(他フェーズは固定)
- **画面モデル: 1画面ホットシート切替**（決定済）
- **デッキ: 各自が別デッキ**（決定済 / `PartyDeckController` が人数分の `PlayerDeck`）
- 流れ（実装済）:
  1. ターン順決定（固定 or `randomizeTurnOrder`）
  2. ターン順に1人ずつ、自分のデッキから `drawCount`(3) 枚ドロー
  3. `selectTimeLimit`(15s) 内に選択。タイムアウトはカーソル位置を自動確定
  4. 全員完了 → `AllSelected` 発火 → ActionCommand へ
- 一番下の長方形 = 全員共有HP（`SharedHpGaugeUI`、Dodge 側で生成）
- 右上の扇型スペース = 別仕様「環境」用（本システム未使用）
- ボスの登場演出はプレイヤー/状況ごとに変化（未実装）

### 未実装 / 保留
- 「待機中の覗き見」: ホットシートは逐次のため本来同時待機が無い。
  ネットワーク/スプリット移行時に再検討（現状はスキップ）
- タイムアウト時の挙動詳細（現状: カーソル位置のカードを自動確定）
- ボス登場演出のバリエーション

## 8. 次段階（未実装・データのみ保持）

- `CardEffectType` の戦闘適用ロジック（DodgePhase の被弾追跡 / ActionCommand 成功数と連携）
- ActionCommandPhase 本体（カテゴリ別ミニゲーム）
- 選択カードの ActionCommand への受け渡し（`CardSelectPhase.Selections[playerIndex]` を参照する想定）
- 難度修正の戦闘反映（弾幕速度・パターン強度等へのマッピング）

---

## 9. 既知の設計判断

- カード UI は **Legacy Text + OS 動的フォント**(`Font.CreateDynamicFontFromOSFont` で Yu Gothic UI 等)。和文を確実表示でき TMP の Essential Resources 等のセットアップ不要。縁取りは `Outline` コンポーネント。TMP は実行時 OS→TMP フォント生成が和文グリフを取り込めず□化したため不採用
- カード効果ロジックは enum + 説明データとして保持し、戦闘への適用は分離（安全に土台を固める方針）
- 強化は「難度と威力を強化レベルで上昇」方式（per-upgrade 増分を Inspector 調整）
- UI は runtime 生成（DodgePhase の HpGauge 等と同方針。将来 Prefab 化候補）
- `== null` 維持・Editor Hint 無視は既存方針を踏襲
