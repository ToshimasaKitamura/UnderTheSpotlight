# UnderTheSpotlight 設計仕様書 索引

戦闘内システム(下層)から、メタ進行(上層)まで。バイブコーディング時の参照用。

---

## レイヤー俯瞰

```
┌────────────────────────────────────────────────────────┐
│              ステージマップ(上位ループ)                       │
│   - 経験値/ボムの獲得・蓄積                                  │
│   - キャラレベルアップ → パッシブ・進化                       │
│   - カード強化 (永続)                                      │
└──────────────┬─────────────────────────────────────────┘
               │ ステージ突入 / 戻り
               ▼
┌────────────────────────────────────────────────────────┐
│              1戦闘 (BattleStateMachine)                  │
│   CardSelect → PartSelect → ActionCommand → Dodge       │
└────────────────────────────────────────────────────────┘
```

---

## 仕様書一覧

### 上位レイヤー(メタ進行)
- [StageMap_Design.md](StageMap_Design.md) — ステージマップ / 連戦 / 報酬 / 解放
- [CharacterPassive_Design.md](CharacterPassive_Design.md) — キャラレベル / パッシブ / 進化
- [CardMetaUpgrade_Design.md](CardMetaUpgrade_Design.md) — 永続カード強化 + テキスト変化

### 戦闘内システム
- [CardSystem_Design.md](CardSystem_Design.md) — カード/デッキ/CardSelect フェーズ
- [PartDestruction_Design.md](PartDestruction_Design.md) — 部位破壊 / 部位選択 / 予告セリフ
- [ActionCommand_Design.md](ActionCommand_Design.md) — グループ式ミニゲーム / 即時命中演出
- [DodgePhase_Design.md](DodgePhase_Design.md) — 弾幕回避 / ボス挙動 / プレイヤー操作交代
- [Bomb_Design.md](Bomb_Design.md) — 援護ボム

### ツール・運用
- [CsvWorkflow.md](CsvWorkflow.md) — Excel/CSV ⇔ ScriptableObject 連動

---

## 実装ステータス(2026-06-01 時点)

| レイヤー | 範囲 | 状態 |
|---|---|---|
| 戦闘内: CardSelect | ホットシート 4人選択 | ✅ |
| 戦闘内: PartSelect | 既存 Enemy にヒットボックス枠オーバーレイ | ✅ |
| 戦闘内: ActionCommand | グループ式ボタン押し + 即時命中演出 | ✅ |
| 戦闘内: Dodge | 弾幕 + 部位破壊効果 (WeakenNextAttack/DisableMovement) | ✅ Step B |
| 戦闘内: 予告セリフ | Dodge 開始前にでかい敵で 2.5 秒 | ✅ |
| メタ: ステージマップ | 仕様書のみ | 📝 設計済 |
| メタ: キャラレベル/パッシブ | 仕様書のみ | 📝 設計済 |
| メタ: カードメタ強化 | 仕様書のみ | 📝 設計済 |

---

## 設計原則(共通)

- **数値はコードにハードコードしない**。GameTuning / CSV / Inspector で管理
- **メタ進行はセーブ対象、戦闘進行は揮発**(原則)
- **新規キャラ/能力/カードは「定義 → CSV → ロード」の流れ**で追加できるよう一貫させる
- **既存のキャラ/敵を流用**(プロシージャル描画で代用しない)
