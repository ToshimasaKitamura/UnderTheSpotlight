# キャラクターレベル + パッシブ 設計仕様書

各プレイヤーキャラに永続レベルがあり、レベルアップで進化・パッシブ強化・新カード解放が起きる。
パッシブは戦闘中に自動で発動する能力。

---

## 1. ユーザー提示仕様

- **レベルが上がるごとに、キャラが進化・パッシブ強化/追加・カードが買えたりする**
- **パッシブ = プレイヤーが自動で発動する能力**、各キャラごとに効果が異なる
- パッシブは **付け替え可能な装備** でもある(複数候補から選ぶ)
- パッシブの発動は **確率で決まる** ものもある
- 例(陽の擬人化):
  - パッシブ: 「カードを破棄するたびに、自分の攻撃力が少し上昇」
  - 進化後: 「カードを破棄するたびに、全員の攻撃力が上昇」

---

## 2. データモデル

### `CharacterDefinition` (ScriptableObject)
| フィールド | 意味 |
|---|---|
| id, displayName | 内部識別子・表示名 |
| baseSprite | 初期スプライト |
| evolutionStages | List<EvolutionStage>(レベル閾値で進化) |
| innatePassiveId | 初期から持っているパッシブ ID(進化で強化される対象) |

### `EvolutionStage` ([Serializable])
| フィールド | 意味 |
|---|---|
| requiredLevel | この進化に必要なレベル |
| evolvedSprite | 進化後のスプライト |
| upgradedPassive | 進化に伴って強化される(または差し替わる)パッシブ ID |
| unlockCardId | 解放されるカード ID(任意) |

### `PassiveData` (ScriptableObject)
| フィールド | 意味 |
|---|---|
| id, displayName, description | パッシブ情報 |
| trigger | PassiveTrigger 種別(enum: OnCardDiscard / OnTurnStart / OnDodgeHit / OnAttackLand 等) |
| effect | PassiveEffect 種別(enum: BoostOwnAttack / BoostAllyAttack / HealOnDodge / ExtraBomb 等) |
| amount | 効果量 |
| probability | 発動確率(0..1)、1 で必中 |
| target | Self / AllAllies / Random(対象スコープ) |
| icon | UI 用アイコン(任意) |

### `CharacterProgress` (シリアライズ可能 / セーブ対象)
プレイヤー1人 (キャラ1人) ごとに保持:
| フィールド | 意味 |
|---|---|
| characterId | 紐づくキャラ ID |
| currentLevel | 現在レベル |
| accumulatedExp | 累積経験値(レベルアップ閾値で次レベルへ) |
| equippedPassives | List<string>(装備中のパッシブ ID。複数スロット想定) |
| unlockedPassives | List<string>(装備候補プール) |
| unlockedCards | List<string>(マップ画面で買える / デッキ編成に入れられる) |

### `PartyProgress` (シーン常駐 / セーブ対象)
全プレイヤー分の `CharacterProgress[]` を束ねる。
ステージクリア時にここを更新。

### `PassiveTrigger` (enum)
- `OnCardDiscard` — カード使用 or 破棄時
- `OnTurnStart` — ターン開始時(CardSelect 入場時)
- `OnDodgeHit` — Dodge で被弾した瞬間
- `OnDodgeAvoid` — ギリギリ回避(将来定義)
- `OnAttackLand` — ActionCommand で命中させた瞬間
- `OnBossKilled` — ボス撃破時
- `OnTurnRoundComplete` — Dodge で全員 1 周操作完了時

### `PassiveEffect` (enum)
- `BoostOwnAttack` — 自攻撃力 +amount
- `BoostAllyAttack` — 味方全体攻撃力 +amount
- `HealParty` — 共有 HP +amount
- `GrantBomb` — ボム +amount
- `ReduceIncomingDamage` — 被ダメ -amount
- `IncreaseCardDraw` — 次のドロー +amount

---

## 3. 戦闘ループへの組み込み

### `PassiveRuntime` (MonoBehaviour、シーン常駐)
- 各 `CharacterProgress` の `equippedPassives` を購読
- 戦闘システム側のイベントを聞き、Trigger 一致で Effect を実行
- 確率発動: `Random.value < passive.probability`

イベントソース例:
| イベント | 発火元 |
|---|---|
| カード破棄 | `PlayerDeck.DiscardHand()` から発火する OnCardDiscarded(playerIndex) |
| ターン開始 | `CardSelectPhase.BeginPlayerTurn` |
| 被弾 | `SharedHpController.OnHpChanged` (減少時) |
| 命中 | `ActionCommandPhase.HandlePlayerAttack` |
| ボス撃破 | `EnemyRuntimeState` 部位全破壊 or 死亡フラグ |

`PassiveRuntime` がイベントを購読、各プレイヤーのパッシブを評価:
```csharp
void OnCardDiscarded(int playerIndex) {
    var prog = partyProgress.Get(playerIndex);
    foreach (var passiveId in prog.equippedPassives) {
        var p = passiveDb.Get(passiveId);
        if (p.trigger != PassiveTrigger.OnCardDiscard) continue;
        if (Random.value > p.probability) continue;
        ApplyEffect(playerIndex, p);
    }
}
```

---

## 4. レベルアップ計算

`accumulatedExp >= GetExpForNextLevel(currentLevel)` で発火:
- currentLevel++
- accumulatedExp -= 閾値
- EvolutionStage[currentLevel] があれば進化処理
  - スプライト差し替え
  - 持参パッシブ強化(`upgradedPassive` で id 差し替え)
  - unlockCardId があれば `unlockedCards` に追加

経験値テーブル(初期案、`GameTuning.expForLevel`):
| level | 必要 exp(累積でなく次のレベルまで) |
|---|---|
| 1 → 2 | 10 |
| 2 → 3 | 25 |
| 3 → 4 | 50 |
| 4 → 5 | 90 |

---

## 5. UI

### マップ画面のキャラパネル
- 4人それぞれにスロット表示: アイコン + level + EXP バー + 装備パッシブアイコン
- クリック/カーソルで詳細(装備変更画面へ)

### パッシブ装備変更画面
- unlockedPassives 一覧から選んで equippedPassives に割り当て
- スロット数は固定(例: 2スロット)
- 重複装備不可

### 戦闘中のパッシブ発動表示
- 発動した瞬間にプレイヤーアイコン上に小さく
  「[パッシブ名] 発動!」
  を 0.7 秒ポップ
- 効果(攻撃力 UP 等) はバナー or アイコンに反映

---

## 6. CSV 連動

`characters.csv`:
```
id,displayName,baseSpriteRef,innatePassiveId
sun,陽,Player_4,sun_discard_self_boost
girl,娘,Player_0,girl_quickdraw
witch,魔女,Player_6,witch_chance_heal
```

`character_evolutions.csv`:
```
characterId,requiredLevel,evolvedSpriteRef,upgradedPassiveId,unlockCardId
sun,3,Player_4_evolved,sun_discard_all_boost,
sun,5,Player_4_evolved,sun_discard_all_boost,solar_flare
```

`passives.csv`:
```
id,displayName,description,trigger,effect,amount,probability,target
sun_discard_self_boost,陽光のひらめき,カードを破棄するたびに自分の攻撃力が少し上がる,OnCardDiscard,BoostOwnAttack,1,1.0,Self
sun_discard_all_boost,太陽の祝福,カードを破棄するたびに全員の攻撃力が上がる,OnCardDiscard,BoostAllyAttack,1,1.0,AllAllies
witch_chance_heal,魔女の気まぐれ,ターン開始時 30% でパーティHP回復,OnTurnStart,HealParty,2,0.3,AllAllies
```

---

## 7. MVP 範囲

**Step A — データのみ**
- CharacterDefinition / EvolutionStage / PassiveData (SO) 定義
- CSV/Importer で読み込み
- 戦闘には反映しない

**Step B — パッシブ最小 1 種**
- PassiveRuntime + イベント購読の枠
- OnCardDiscard → BoostOwnAttack のみ動かす
- 戦闘中の発動ログ + 効果反映(攻撃力修正)

**Step C — レベルアップ + 進化**
- accumulatedExp / currentLevel 加算
- 進化スプライト差し替え
- レベルアップ通知 UI

**Step D — 装備変更画面 + 確率発動 + 各パッシブ実装**
- マップ画面に装備 UI
- 確率発動分の評価
- パッシブ各種実装

---

## 8. 関連ドキュメント

- [StageMap_Design.md](StageMap_Design.md) — ステージクリアでレベルアップする上位ループ
- [CardMetaUpgrade_Design.md](CardMetaUpgrade_Design.md) — パッシブと同様、カード強化でテキストも変わる仕様
- [CardSystem_Design.md](CardSystem_Design.md) — 既存カードシステム(unlockedCards から購入する対象)
