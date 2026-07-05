# カードのメタ強化 設計仕様書

ボス撃破で得た経験値を使い、**マップ上で各プレイヤーが自分のカードを永続強化** する。
強化は数値だけでなく **説明文(口語)が分かりやすく変わる** ことが特徴。

---

## 1. ユーザー提示仕様

- ボスを一体撃破するごとに、もらえる経験値で **カードを強化できる**
- 強化は **マップ上で、各自に行う**
- 強化の内容は **パッシブ同様、数値が上がるだけでなく、わかりやすく効果が強くなるように表現する**
- 例:
  - 攻撃カード(初期): 「相手にダメージを与え、次のターンの攻撃力がちょっと上がる」
  - 強化後:           「相手にダメージを与え、次に行動する人の攻撃力がとても上がる」

---

## 2. 既存システムとの区別(重要)

| 既存 (`CardInstance.UpgradeLevel`) | 新 (`CardMetaUpgrade`) |
|---|---|
| 戦闘内のデッキ周回ボーナス(1ループごとに +1) | プレイヤーが永続的に強化する経験値投入 |
| 戦闘終了でリセット | セーブ対象、戦闘またいで保持 |
| `EffectiveDifficulty` / `EffectivePower` の算出に使用 | 同じく実効値に乗るが、ベースが異なる |

**統合方針**: `CardInstance` の実効値計算を
```
EffectivePower = data.basePower + powerPerUpgrade * (battleUpgradeLevel + metaUpgradeLevel)
```
のように **両方の合算** にする。説明文の表現は `metaUpgradeLevel` のみで切り替え。

---

## 3. データモデル

### `CardUpgradeTier` ([Serializable], inside CardData)
カード1種ごとに「強化段階の表現」を定義する。
| フィールド | 意味 |
|---|---|
| tierLevel | この段階の番号(0 = 初期、1, 2, ... と上昇) |
| descriptionOverride | この段階での説明文(差し替え)。空なら baseDescription を使う |
| extraPowerAtThisTier | この段階で追加される威力(累積でなく差分) |
| extraDifficultyAtThisTier | 同 難度 |
| requiredExpToReach | この段階に到達するための経験値コスト |
| iconOverride | この段階用アイコン(進化感) |

### `CardData` 拡張
```
public List<CardUpgradeTier> upgradeTiers;
```
- tiers[0] = ベース(必ず存在)
- tiers[N] = N 段階目
- インスペクタで増減

### `CardMetaProgress` (シリアライズ可能 / セーブ対象)
プレイヤー1人ごとに保持:
| フィールド | 意味 |
|---|---|
| ownerPlayerIndex | どのプレイヤーのものか |
| perCardLevels | Dictionary<string, int>(cardId → 現在のメタ強化レベル) |

### `RunProgress.accumulatedExp` から消費
マップ画面の強化メニューで `requiredExpToReach` 分を消費して level++

---

## 4. CardInstance 実効値の合算

```csharp
public int EffectivePower {
    get {
        int meta = metaProgress?.GetLevel(Data.id) ?? 0;
        int total = Data.basePower;
        // 既存: battle upgrade 分(デッキ周回)
        total += Data.powerPerUpgrade * UpgradeLevel;
        // 新: meta 強化分(tiers の累積)
        for (int t = 1; t <= meta && t < Data.upgradeTiers.Count; t++) {
            total += Data.upgradeTiers[t].extraPowerAtThisTier;
        }
        return total;
    }
}

public string EffectiveDescription {
    get {
        int meta = metaProgress?.GetLevel(Data.id) ?? 0;
        if (meta > 0 && meta < Data.upgradeTiers.Count
            && !string.IsNullOrWhiteSpace(Data.upgradeTiers[meta].descriptionOverride))
            return Data.upgradeTiers[meta].descriptionOverride;
        return Data.description;
    }
}
```

`CardHandView` は `EffectiveDescription` を引いて表示 → 強化後の文章がそのまま見える。

---

## 5. UI(マップ画面のカード強化)

### 流れ
1. マップ画面 →「カード強化」ボタン
2. プレイヤー選択(P1〜P4)
3. そのプレイヤーが所有するカード一覧
   - 各カード: 現在 tier / 次 tier への必要経験値 / 強化後説明プレビュー
4. 「強化」ボタンで `accumulatedExp -= cost`、`metaProgress.perCardLevels[cardId]++`
5. 「強化後はこうなる」のテキストが大きく出る → ユーザーに変化を実感させる

### 演出
- 強化決定時、説明文の差分を **タイプライター風** にハイライト
- 数値が上がる箇所は黄色フラッシュ

---

## 6. CSV 連動

`card_upgrades.csv` (cards.csv とは別ファイル):
```
cardId,tierLevel,descriptionOverride,extraPower,extraDifficulty,requiredExp,iconRef
attack_basic,0,,0,0,0,
attack_basic,1,"相手にダメージを与え、次に行動する人の攻撃力がちょっと上がる",2,1,10,
attack_basic,2,"相手にダメージを与え、次に行動する人の攻撃力がとても上がる",5,2,30,
heal_basic,0,,0,0,0,
heal_basic,1,"即時HPを少量回復し、避けるたびに少量回復する",1,0,10,
heal_basic,2,"即時HPを中量回復し、避けるたびにしっかり回復する",3,1,30,
```

- tierLevel 0 行は省略可(自動で空 tier を入れる)
- 同 cardId が複数行 → tier の昇順で並べる
- インポート時に `CardData.upgradeTiers` を組み立てる

---

## 7. セーブ

`PartyMetaProgress` を JSON にして PlayerPrefs / ローカルファイルへ:
```json
{
  "byPlayer": {
    "0": { "cardLevels": { "attack_basic": 1, "heal_basic": 0 } },
    "1": { "cardLevels": { "attack_basic": 2 } }
  }
}
```

---

## 8. MVP 範囲

**Step A — データ層**
- `CardUpgradeTier` + `CardData.upgradeTiers` 追加
- card_upgrades.csv + CsvImporter 拡張
- `CardMetaProgress` + `CardInstance.EffectivePower / EffectiveDescription` の合算

**Step B — 強化 UI 最小**
- マップ画面に「強化」モーダル(プレイヤー選 → カード選 → 強化ボタン)
- accumulatedExp を消費して tier++
- 強化前後の説明文プレビュー(差分演出は後回し)

**Step C — 演出 + セーブ**
- タイプライター差分演出
- PlayerPrefs / JSON セーブ・ロード
- 強化済みカードのアイコンに「★」表示

---

## 9. 関連ドキュメント

- [StageMap_Design.md](StageMap_Design.md) — マップ画面に強化メニューが組み込まれる
- [CardSystem_Design.md](CardSystem_Design.md) — 既存カードシステム + battle upgrade との関係
- [CharacterPassive_Design.md](CharacterPassive_Design.md) — 同じく「効果が強くなるよう表現を変える」方針
- [CsvWorkflow.md](CsvWorkflow.md) — 新規 card_upgrades.csv の取り込み手順
