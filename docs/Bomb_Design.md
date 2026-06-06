# ボム(援護)システム 設計仕様書

弾幕回避中、操作プレイヤー以外のメンバーが「ボム」を発動して操作プレイヤーを援護する仕組み。

---

## 1. ユーザー提示仕様

- 敵の攻撃の最中、**操作プレイヤー以外**の人がボムを使用できる
- ボムを使うと、**画面上にある弾を全て削除** し、操作プレイヤーを手助けする
- **数に制限あり**(消費型リソース)

UI 例: HP バー周辺に各プレイヤーごとのボム残数アイコン(色違い)が並ぶ。

---

## 2. データモデル(提案)

### `BombController` (MonoBehaviour, 永続)
| フィールド | 意味 |
|---|---|
| initialBombsPerPlayer | 各プレイヤーの初期ボム数 |
| bombs[] | 各プレイヤーの現在ボム数 |
| event BombUsed(int playerIndex) | 使用時イベント |
| event BombsChanged | 表示更新通知 |

### Methods
- `bool TryUseBomb(int playerIndex)` — 残あり&使用可能なら消費して true
- `void RefillAll()` — フェーズ開始時にリセット(必要なら)
- `int GetBombs(int playerIndex)`

### GameTuning 連動
- `bombsPerPlayer` (int) を GameTuning に追加(例: 2)

---

## 3. 入力ハンドリング(ホットシート前提)

弾幕中の入力:
- 数字キー `1-4` + 移動キー = 操作(現状の仕様)
- **数字キー `1-4` を単独で(移動なしで)押下 = その番号のプレイヤーがボム使用**(操作中プレイヤー以外で残数あり時)

別案(より直感的):
- `Q/W/E/R` 等の専用ボムキーを 1P〜4P 用に割り当て
- どちらが良いか要確認

---

## 4. 効果

- `DanmakuManager.ClearAllBullets()` 呼び出し(既存メソッド利用)
- ボム発動演出: 画面フラッシュ + 短い拡散リング(中央フィールド全体)
- 操作プレイヤーの一時無敵(0.5秒程度)も検討
- 使用プレイヤーのボム数を 1 減算 → UI 更新

---

## 5. UI(提案)

HP バーの左右または下に、プレイヤーカラーのボムアイコンを残数分並べる:
```
[HP ###########____]
1P: ●●  2P: ●●  3P: ●  4P: ●●
```
- 使用するとアイコンが 1 つ消える(フェードアウト演出)
- 残 0 で半透明グレー表示

---

## 6. 既存システムとの接続

- **DodgePhase**: Tick 内で「数字キー単独押下」を判定 → `BombController.TryUseBomb` → 効果発動
- **DanmakuManager**: `ClearAllBullets()` 既存
- **新規 `BombGaugeUI`**: 残数アイコンを表示(SharedHpGaugeUI と同居)
- **CardSelectPhase / ActionCommandPhase**: ボム残数は表示するだけ(使用不可)
- **GameTuning**: `bombsPerPlayer` 追加で Excel 調整可

---

## 7. CSV 連動

`game_tuning.csv` に行追加:
```csv
bombsPerPlayer,2,各プレイヤーは初期【○個】のボムを所持できる
bombInvulnerabilityDuration,0.5,ボム使用後【○秒】間 操作プレイヤーは無敵になる
```

---

## 8. MVP 範囲

**Step A — 最小機能**
- BombController + GameTuning に `bombsPerPlayer` 追加
- DodgePhase で「数字キー単独押下」を検知 → ClearAllBullets
- 簡易 UI(HPバー下にボム残数テキスト)

**Step B — 通常**
- ボム使用時のフラッシュ演出
- アイコン UI(色付き丸)
- 操作プレイヤーの短時間無敵

**Step C — 高度**
- 専用キー(Q/W/E/R)対応
- フェーズ間でのボム回復ルール
- ボム獲得アイテム
