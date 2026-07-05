# ステージマップ 設計仕様書

戦闘の上位にあるメタ進行レイヤー。Mario Bros Wii 風のワールドマップ上を移動し、
各ステージで複数のボスを倒してクリアする。

---

## 1. ユーザー提示仕様

- ステージクリア方式のマップ(Mario 風)
- **1ステージにつき、ボスが複数いる**(連戦)
- ボス一体撃破するごとに **カードのレベルを上げられる「経験値」や「ボム」** が手に入る
  - ノーダメ等の条件で **もらえる量が増加するボーナス** も予定
- 全ボス撃破でステージクリア
- ステージクリアで **使ったキャラクターのレベルが上昇**

---

## 2. データモデル(提案)

### `WorldMapData` (ScriptableObject)
| フィールド | 意味 |
|---|---|
| id, displayName | ワールド識別子 / 表示名 |
| stages | List<StageEntry>(ステージ並び順 + 接続情報) |
| bgmKey, mapBackgroundSprite | マップ画面の見た目 |

### `StageEntry` ([Serializable] inside WorldMapData)
| フィールド | 意味 |
|---|---|
| stageId | ステージ識別子 |
| stageData | 中身(`StageData`) |
| nodePosition | マップ上のノード座標(Vector2、0..1 正規化) |
| prerequisites | 解放条件(他ステージ ID のリスト = それらをクリア済みで解放) |

### `StageData` (ScriptableObject)
| フィールド | 意味 |
|---|---|
| id, displayName | ステージ識別子 / 表示名 |
| bosses | List<EnemyData>(順番に登場する複数ボス) |
| baseExpReward | 1体撃破ごとの基本経験値 |
| baseBombReward | 1体撃破ごとの基本ボム取得数 |
| clearBonus | StageClearBonus(全撃破時の追加報酬) |
| environmentSprite | 戦闘背景(任意) |

### `StageClearBonus` ([Serializable])
| フィールド | 意味 |
|---|---|
| extraExp | クリア時の経験値ボーナス |
| extraBombs | クリア時のボム追加 |
| unlockCardId | 強化可能カード解放(任意) |

### `BossRewardBonus` ([Serializable])
ボス撃破時に判定する**条件ボーナス**:
| 条件 | 説明 |
|---|---|
| `NoDamage` | パーティ HP が一度も減らなかった |
| `QuickClear` | N秒以内に撃破 |
| `AllCardsUsed` | プレイヤー全員が攻撃カードを命中させた |
| `NoBombUsed` | ボムを1個も使わなかった |
増分は `(条件 → 倍率/加算)` のテーブル。
`RunProgress.AccumulateReward(BossRewardEvaluation)` で集計。

### `RunProgress` (シーン常駐 MonoBehaviour)
| フィールド | 意味 |
|---|---|
| currentStage | プレイ中のステージ |
| currentBossIndex | 連戦の何体目か |
| accumulatedExp | このランで貯めた経験値(マップ復帰時に CharacterProgress へ反映) |
| accumulatedBombs | 取得ボム |
| noDamageThisStage | ステージ通しでノーダメ達成中か(被弾で false) |
| bombsUsedThisStage | このステージで使ったボム数 |
| clearedStages | クリア済みステージ ID 集合(セーブ対象) |

---

## 3. フロー

```
ワールドマップ
  └─ ノード(ステージ) 選択
     └─ ステージ突入 (StageData.bosses[0] でバトル開始)
        ├─ CardSelect → PartSelect → ActionCommand → Dodge → 撃破判定
        ├─ 撃破: 経験値/ボム加算 + bossIndex++
        ├─ 次のボス存在: 続行(休憩無しで次の CardSelect、HP は引き継ぎ)
        └─ 全ボス撃破: クリアボーナス計算 → マップに戻る
   └─ マップ戻り
      ├─ キャラレベル UP 判定([CharacterPassive_Design.md])
      ├─ カード強化画面に行ける([CardMetaUpgrade_Design.md])
      └─ 次ステージ解放
```

連戦中の引き継ぎ:
- **共有 HP は引き継ぎ**(SharedHpController を保持)
- **ボム残量も引き継ぎ**(BombController を保持)
- 山札は **リセット**(各ボスごとに新規ドロー = カード選択フェーズの自然な動作)

---

## 4. ノードの解放ロジック

- StageEntry.prerequisites = 空 → 最初から解放
- 全 prerequisites が `RunProgress.clearedStages` に含まれる → 解放
- 解放されたノード = マップ上で押せる/光る、未解放 = 暗いカギマーク

---

## 5. 既存システムとの接続

| 既存 | 変更点 |
|---|---|
| `BattleStateMachine` | 戦闘終了後の遷移先を **マップ復帰 or 次のボス** に分岐 |
| `SharedHpController` | ステージ間で持続(マップ復帰時にリセット) |
| `BombController` | 同上 |
| `EnemyRuntimeState` | 各ボスごとに `enemyData` を差し替え(現状は固定 hamster) |
| `PartyDeckController` | ステージ内は引き継ぎ無し(各バトルごとに山札再構築) |

---

## 6. UI

### マップ画面
- 上半分: 背景画像(草原・洞窟等のステージ並び)
- ノード = アイコン(クリア済み/解放中/未解放で色分け)
- カーソルキーでノード移動、Enter で突入
- 解放済みノードは前後ノードへの矢印描画

### 連戦中の小演出
- ボス撃破 → 「敵撃破! +N exp / +M bombs」フラッシュ(2秒) → 次ボス入場アニメ

### マップ復帰時
- 「クリア!」演出 + 報酬まとめ表示
- レベルアップ通知(キャラごと)

---

## 7. CSV / GameTuning 連動

`stages.csv` 案:
```
stageId,displayName,bossId,bossOrder,nodeX,nodeY,prerequisites,baseExp,baseBombs,clearExtraExp
stage1,森の入口,hamster,0,0.2,0.5,,5,1,10
stage1,森の入口,hamster_alt,1,0.2,0.5,,8,1,
stage2,森の奥,bear,0,0.4,0.5,stage1,10,2,20
```
(同 stageId の複数行 = ボス連戦の各ボスを表す)

GameTuning 拡張:
- `noDamageBonusMul` = 1.5
- `quickClearThresholdSec` = 60
- `quickClearBonusMul` = 1.3
- `noBombBonusExp` = 5

---

## 8. MVP 範囲(段階分け)

**Step A — マップ最小 — ✅ 実装済み(視覚仕上げ4パス完了)**
- ✅ `StageData` / `WorldMapData` (ScriptableObject 定義)
- ✅ `RunProgress` (シーン常駐、現在ステージ・連戦インデックス・累積exp/bombs)
- ✅ `WorldMapPhase` (IBattlePhase) + `WorldMapView` (ノードボタン UI、←→で選択 / Enter で突入)
- ✅ `BattleStateMachine.State.WorldMap` 追加 + initialState で起動状態を切替可能
- ✅ Dodge 終了時に `RunProgress.OnBossDefeated()` → 連戦継続なら CardSelect、完全クリアなら WorldMap へ
- ✅ ステージ突入時に `EnemyRuntimeState.enemyData` を選ばれた最初のボスへ差し替え
- ✅ 連戦中のボス差し替えにも対応(次ボスは `RunProgress.CurrentBoss` 経由)
- ✅ サンプルアセット: `Stage_1` (1ボス) / `Stage_2` (連戦2ボス) / `WorldMap_1` (2ノード並び)
- 🔒 ノーダメ/QuickClear 等のボーナス判定は未実装(Step B 範囲)
- 🔒 prerequisites による解放ロジックは無視(常時解放扱い、Step C で実装)
- 🔒 セーブ/ロードなし(再起動でリセット、Step C 範囲)

### 視覚仕上げ(4パス反復)
- 配色: 黄+黒(深ゴールド/明ゴールド/ホット/ミュート/クリーム白の多階層)
- フォント: ヘッダ明朝(Yu Mincho)/ 本文ゴシック / 漢字 thin space で「第 一 世 界」
- ノード: 10-spike スター形(outer/inner/core 3層 + 中央数字)
- パス: ベジエ曲線+サインカーブ装飾ドット
- ヘッダ装飾: [ダイヤ→線→ダイヤ→線]を両側に
- 浮遊選択カーソル: 黒枠+クリーム内側のダイヤ、ホバー+回転揺れ
- 背景: 放射ゴールド光(2層、互いに逆回転) + 48個リング状散布星(中央バンド回避)
- アール・デコ風四隅装飾(L字+ダイヤ)
- 選択ノード周辺にスパークル粒子(0.2〜0.5秒間隔で生成、拡大+フェード)
- クリア印: 外側右上に大きな★(黒枠+ゴールド本体)
- HUD: フレーム+4隅ダイヤ+EXP/BOMB アイコン
- 確定エフェクト: 選択ノード爆発+12個スパークル+画面ゴールドフラッシュ

**Step B — 連戦 + 報酬**
- StageData.bosses で複数ボス対応
- HP/ボム引き継ぎ
- 撃破ごとの経験値/ボム加算 + UI 表示
- ノーダメ等のボーナス判定

**Step C — 解放 + セーブ**
- prerequisites による解放ロジック
- clearedStages の PlayerPrefs / JSON 保存
- マップ復帰時のレベルアップ通知

**Step D — 演出**
- ノード移動アニメ
- ステージ背景の切り替え
- ボス入場の差別化

---

## 9. 関連ドキュメント

- [CharacterPassive_Design.md](CharacterPassive_Design.md) — マップ復帰時のレベルアップ後の挙動
- [CardMetaUpgrade_Design.md](CardMetaUpgrade_Design.md) — マップで経験値を使ってカード強化
- [Bomb_Design.md](Bomb_Design.md) — ボム残量の引き継ぎ
- [PartDestruction_Design.md](PartDestruction_Design.md) — ボスごとの部位定義
