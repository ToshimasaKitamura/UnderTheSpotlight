# ドキュメント構成

このフォルダには、プロジェクトの仕様書とドキュメントが格納されています。

## フォルダ構成

- **Specifications/** - 仕様書（実装の基準となる文書）
  - ゲーム設計、システム仕様、データ構造など
- **Design/** - 設計書（予定）
  - クラス設計、アーキテクチャ設計など
- **Reference/** - リファレンス（予定）
  - API仕様、データフォーマットなど

## 重要な原則

### 仕様書の位置づけ
1. **仕様書が真実** - コードと仕様が矛盾する場合、仕様書を基準とする
2. **実装前に読む** - 新機能開発時は必ず関連仕様を確認
3. **実装後に照合** - 作業完了後、仕様と実装の整合性を確認
4. **ずれを検出したら更新** - 仕様書または実装を修正し、同期を保つ

### AI開発時のルール
Claude Codeが開発を行う際は、以下の手順を遵守：
1. **タスク開始時** - Docs/Specifications/ 内の関連仕様を読み取る
2. **実装中** - 仕様に従って開発を進める
3. **完了時** - 実装が仕様通りか照合する
4. **ずれ検出時** - ユーザーに報告し、どちらを修正すべきか確認

## 現在のドキュメント

### Specifications/ - 仕様書（実装の基準）
- [GAME_SPECIFICATION.md](Specifications/GAME_SPECIFICATION.md) - ゲーム全体仕様（最優先）
- [GAME_FLOW.md](Specifications/GAME_FLOW.md) - ゲームフロー、シーン遷移
- [FOLDER_STRUCTURE.md](Specifications/FOLDER_STRUCTURE.md) - プロジェクトフォルダ構成

### Design/ - 設計書（個別システムの詳細設計）
- [ActionCommand_Design.md](Design/ActionCommand_Design.md) - アクションコマンド設計
- [Bomb_Design.md](Design/Bomb_Design.md) - ボムシステム設計
- [CardMetaUpgrade_Design.md](Design/CardMetaUpgrade_Design.md) - カード強化設計
- [CardSystem_Design.md](Design/CardSystem_Design.md) - カードシステム設計
- [CharacterPassive_Design.md](Design/CharacterPassive_Design.md) - キャラクターパッシブ設計
- [CsvWorkflow.md](Design/CsvWorkflow.md) - CSVインポートワークフロー
- [DodgePhase_Design.md](Design/DodgePhase_Design.md) - 回避フェーズ設計
- [PartDestruction_Design.md](Design/PartDestruction_Design.md) - 部位破壊システム設計
- [StageMap_Design.md](Design/StageMap_Design.md) - ステージマップ設計

### Root/ - プロジェクト全体
- [DEVELOPMENT_GUIDE.md](DEVELOPMENT_GUIDE.md) - 開発ガイドライン（必読）
- [README.md](README.md) - このファイル

## 今後追加予定のドキュメント

- **Specifications/BATTLE_SPECIFICATION.md** - バトルシステム詳細仕様
- **Specifications/UI_SPECIFICATION.md** - UI/UX仕様
- **Specifications/TUNING_SPECIFICATION.md** - ゲームバランス調整値
