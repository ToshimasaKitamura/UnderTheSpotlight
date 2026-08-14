# ドキュメント構成 ／ 仕様の索引（矛盾を防ぐ運用）

このフォルダはプロジェクトの仕様・設計・開発ドキュメントの置き場です。
**「このゲームの仕様を矛盾なく記述する」ための運用**を以下に定めます。

## 大原則: 単一所有（Single Source of Truth）

1. **1トピック＝1正典**。同じ事実を複数の文書に書かない。他所からは**リンクで参照**する（再掲しない）。
2. **仕様書が真実**。コードと仕様が食い違ったら、正しい方に合わせて**両方を同期**する（放置しない）。
3. **挙動を変えたら、その正典を必ず更新する**（更新しない変更は未完了）。
4. **矛盾は自動検出する** → `bash Tools/check_spec_consistency.sh`（下記）。

## トピック → 正典（ここを直す）

| トピック | 正典（唯一の場所） |
|---|---|
| ゲーム全体像・プレイヤー数・勝敗 | [Specifications/GAME_SPECIFICATION.md](Specifications/GAME_SPECIFICATION.md) |
| シーン遷移・各シーンの操作 | [Specifications/GAME_FLOW.md](Specifications/GAME_FLOW.md) |
| オンライン対戦（部屋/コード/参加枠） | [Design/Networking_Design.md](Design/Networking_Design.md) |
| フォント・テーマカラー（見た目） | [Specifications/デザインテーマ.md](Specifications/デザインテーマ.md) |
| フォルダ構成・ファイル配置 | [フォルダ構成.md](フォルダ構成.md) |
| 各システムの詳細設計 | `Design/<システム>_Design.md`（下表） |
| ビルド対象シーンと順序 | `ProjectSettings/EditorBuildSettings.asset`（コードが真実） |
| ゲームバランス数値 | `Assets/Data/csv/*` ＋ [Design/CsvWorkflow.md](Design/CsvWorkflow.md) |
| キャラとスプライトの対応 | [CharacterData_Sprite_Mapping.md](CharacterData_Sprite_Mapping.md) |
| 修正の経緯・既知の罠 | [開発ナレッジ.md](開発ナレッジ.md) |

> 上表に無い新トピックを書くときは、**まず正典をどこにするか決めてこの表に1行足す**。決めずに複数箇所へ書かない。

## Design/ — 個別システム設計（各システムの正典）

- [ActionCommand_Design.md](Design/ActionCommand_Design.md) — アクションコマンド
- [Bomb_Design.md](Design/Bomb_Design.md) — ボム(援護)
- [CardSystem_Design.md](Design/CardSystem_Design.md) — カード/デッキ
- [CardMetaUpgrade_Design.md](Design/CardMetaUpgrade_Design.md) — カード強化
- [CharacterPassive_Design.md](Design/CharacterPassive_Design.md) — キャラLv/パッシブ
- [CsvWorkflow.md](Design/CsvWorkflow.md) — CSV パラメータ管理
- [DodgePhase_Design.md](Design/DodgePhase_Design.md) — 弾幕回避フェーズ
- [Networking_Design.md](Design/Networking_Design.md) — オンライン対戦（ロビー/部屋コード）
- [PartDestruction_Design.md](Design/PartDestruction_Design.md) — 部位破壊
- [StageMap_Design.md](Design/StageMap_Design.md) — ステージマップ

## その他

- [修正PDCA.md](修正PDCA.md) — 修正を Plan→Do→Check→Act で回す仕組み（`/pdca`）
- [DEVELOPMENT_GUIDE.md](DEVELOPMENT_GUIDE.md) — 開発ガイドライン
- [TroubleShooting_SpriteNotVisible.md](TroubleShooting_SpriteNotVisible.md) — 既知のトラブル対処

## 矛盾検出の仕組み

- **`bash Tools/check_spec_consistency.sh`** — リンク切れ / 仕様が参照するシーン・型の実在 / 見出しの二重所有 を検査。違反があれば終了コード 1。
- `.claude/settings.json` の PostToolUse フックで、Write/Edit のたびに自動実行される。
- 仕様と実装がずれていたら、上表の正典を直して同期する。

### AI（Claude Code）が守る手順
1. 実装前: 関連する正典（上表）を読む。
2. 実装後: 挙動を変えたら**その正典を更新**し、`check_spec_consistency.sh` を通す。
3. 迷ったら: どちらを正とするかユーザーに確認してから同期する。
