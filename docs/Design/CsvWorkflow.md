# Excel/CSV パラメータ管理 ワークフロー

ゲーム数値(カード/デッキ/バランス)を Excel で編集し、Unity の ScriptableObject へ取り込むフロー。

---

## 1. 全体像

```
Excel(.xlsx) で編集
   │  「UTF-8 CSV で保存」
   ▼
Assets/Data/csv/*.csv        ← 真の入力
   │  Unity メニュー「UnderTheSpotlight > Reimport from CSV」
   ▼
Assets/Data/Cards/*.asset    (CardData SO)
Assets/Data/StandardDeck.asset など (DeckDefinition SO)
Assets/Data/GameTuning.asset (GameTuning SO)
   │  各 MonoBehaviour が Awake で参照
   ▼
ゲーム挙動に即反映
```

逆方向(現状の SO → CSV)も `UnderTheSpotlight > Export Current to CSV` で可能。

---

## 2. ファイル仕様

### `Assets/Data/csv/cards.csv` — カードの定義
| 列 | 例 | 説明 |
|---|---|---|
| id | `attack_basic` | 識別子(英数+_)。Excel編集中はそのまま、Unity取込時に asset 名にも使う |
| displayName | `アタック` | 表示名 |
| description | `〜大ダメージ` | カード説明。**セル内のカンマは "..." で囲む** |
| category | `0..4` | 0=Attack/1=Defend/2=Heal/3=Buff/4=Special |
| tier | `0..2` | 0=Basic/1=Special/2=Ultimate |
| baseDifficultyModifier | `3` | 難度修正の基本値 |
| basePower | `3` | 威力(現状未表示・将来用データ) |
| difficultyPerUpgrade | `1` | 強化1段ごとの難度増分 |
| powerPerUpgrade | `2` | 強化1段ごとの威力増分 |
| effectType | `0..5` | 0=None/1=AttackNoHitBonus/2=DefendTauntReduce/3=HealImmediateAndOnDodge/4=BuffGeneric/5=SpecialSuccessScaling |
| attackRange | `0..3` or `Single`/`Upper`/`Lower`/`All` | 部位破壊用 攻撃範囲。攻撃以外のカテゴリは `All` (3) 推奨 |
| cardColor | `#FF5959` | カード色 |

### `Assets/Data/csv/enemy_parts.csv` — 敵の部位定義(部位破壊システム)
1行 = 1部位。同じ `enemyId` の行が複数並ぶことで複数部位を表現。
インポート時に `Assets/Data/Enemies/{enemyId}.asset` (EnemyData) が自動生成される。

| 列 | 例 | 説明 |
|---|---|---|
| enemyId | `hamster` | 敵識別子(英数+_)。同名で複数行=同じ敵の部位リスト |
| enemyDisplayName | `ハムスター` | 敵表示名。同じ enemyId なら1行目のもので確定 |
| partId | `head` | 部位識別子(敵内でユニーク) |
| partDisplayName | `頭` | 部位の表示名 |
| zone | `Upper` or `Lower` | 攻撃範囲フィルタ用(`CardAttackRange.Upper/Lower` が狙えるかどうか) |
| maxHp | `3` | 部位HP。攻撃カード命中で減算、0で破壊 |
| effect | `WeakenNextAttack` 等 | 破壊時効果。enum 名 or 整数値(0=None/1=WeakenNextAttack/2=PreventHeal/3=ReducePatternDensity/4=ReducePatternSpeed/5=DisableMovement) |
| hintCue | `「足を狙わせるな！」と…` | この部位を狙わせる演出文(Dodge 中のセリフや動きの説明文) |

### `Assets/Data/csv/decks.csv` — デッキ構成
| 列 | 例 | 説明 |
|---|---|---|
| deckName | `StandardDeck` | デッキ名(同名で複数行=デッキの中身) |
| cardId | `attack_basic` | cards.csv の id を参照 |
| copies | `5` | 同じカードを何枚入れるか |

### `Assets/Data/csv/game_tuning.csv` — ゲームバランス数値
| 列 | 例 | 説明 |
|---|---|---|
| key | `maxHp` | パラメータ名(コード参照用) |
| value | `5` | 値(数値/真偽/区切り付き文字列) |
| description | `【最大HPは○】全員で共有` | プランナー向け説明。**【】内の○が value に対応** |

配列項目（プレイヤー毎の色やセリフ）は **1 行 1 要素** の表形式で書く。例:
```csv
playerColor_1,#FF5959,1Pの色 (#RRGGBB)
playerColor_2,#5C99FF,2Pの色 (#RRGGBB)
playerColor_3,#5CFF66,3Pの色 (#RRGGBB)
playerColor_4,#FFD959,4Pの色 (#RRGGBB)
playerCallout_1,1番さん出番ですッ！,1Pに切り替わったときのセリフ
playerCallout_2,2番さん助けて〜！！,2Pに切り替わったときのセリフ
```
キー名は `playerColor_N` / `playerCallout_N`（N=1 始まり）。インポーターが `tuning.playerColors[N-1]` / `playerCallouts[N-1]` に振り分ける。旧形式の `playerColors`（`;` 区切り 1 セル）も互換で読み込めるが、Export では新形式で書き出される。

**プランナーが新パラメータを追加したいとき**: 新しい行を追加するだけ。例:
```csv
moveStartXSpeed,5,"【ワザ開始時のX速度は○】で移動を開始"
moveDeceleratePerFrame,0.2,"同時に【ワザ中のX減速度は毎F○】で減速する"
```
未知の key は `GameTuning.customParams` に格納され、コードからは `tuning.GetFloat("moveStartXSpeed", 5f)` 等で参照できる。コーダが参照箇所を追加すれば、プランナーは値と説明文を CSV で自由に編集可。

---

## 3. 取り込み手順

1. Excel で `cards.csv` 等を開く(UTF-8 CSV 形式で保存)
2. 値を編集して保存(BOM 有無どちらでも可)
3. Unity に戻る
4. メニュー **UnderTheSpotlight > Reimport from CSV**
5. 完了ダイアログで反映件数を確認 → Play で挙動確認

新規カードを足す場合は `cards.csv` に新しい id 行を追加 → Reimport。`Assets/Data/Cards/<id>.asset` が自動生成されます。デッキ内訳は `decks.csv` に行を追加。

### 既存値を CSV に書き出す
- メニュー **UnderTheSpotlight > Export Current to CSV** で `Assets/Data/csv/*.csv` を現在の SO 値で再生成。
- 初回 Excel 化や、Inspector で直接弄ったあとに Excel と同期させたい時に使う。

---

## 4. CSV ⇄ ScriptableObject 同期方針

- **id(カード)/ deckName(デッキ)が一致**すれば既存 SO を上書き更新
- 一致なしの新規行は新規 SO を作成(`Assets/Data/Cards/<id>.asset` など)
- CSV から削除しても**自動削除はしない**(誤削除防止)。不要な SO は手動で削除

---

## 5. ランタイム参照(GameTuning)

各 MonoBehaviour の Inspector に `Tuning` フィールドがあり、`Assets/Data/GameTuning.asset` を参照済。

| クラス | tuning から反映される値 |
|---|---|
| `SharedHpController` | `maxHp` |
| `PartyDeckController` | `playerCount`, `drawCount` |
| `CardSelectPhase` | `playerCount`, `selectTimeLimit`, `randomizeTurnOrder`, `playerColors` |
| `ActionCommandPhase` | `playerCount`, `roundsPerActionCommand`, `interRoundDelay`, `resultFlashDuration`, `baseTimeLimit`, `timeLimitPerRound`, `speedRampPerRound`, `buttonCountBase`, `difficultyToSpeed`, `difficultyToInterfere`, `minTimeLimit`, `playerColors` |
| `DodgePhase` | `playerTurnDuration`, `playerColors`, `playerCallouts`, `maxHp`(参考) |

`tuning` 未設定なら各クラスのローカル既定値(Inspector で個別調整) が使われる。

---

## 6. 範囲外(Excel 管理しない値)

下記は Inspector で個別管理(Excel に乗せない方針 - シーン固有の数値):
- 各種アニメ秒数(`enemyRiseDuration`, `fieldScaleDuration`, etc.)
- ワールド座標(`enemyOnFieldSpawnPosition`, `leftSideWorldPositions`, etc.)
- スケール(`centralFieldAppearScale`, etc.)
- スプライト/プレハブ参照

これらを Excel 化したい場合は `GameTuning` に項目を追加 + `ApplyTuning()` を拡張する(`docs/CsvWorkflow.md` のステップC方針)。

---

## 7. よくある不具合

- **日本語が文字化け** → Excel が UTF-8 ではなく Shift-JIS で保存している。Excel の「名前を付けて保存 > CSV UTF-8」を使う(`game_tuning.csv` の playerCallouts は和文OK)
- **セル内カンマで列ズレ** → そのセルを `"..."` で囲む(Excel は自動で行う)
- **「Card not found: xxx」警告** → decks.csv の cardId が cards.csv に存在しない。スペルチェック
- **再 Reimport しても反映されない** → Unity を Editor focus → F5(または Assets > Refresh)
