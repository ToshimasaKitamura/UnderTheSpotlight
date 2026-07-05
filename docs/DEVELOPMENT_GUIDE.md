# 開発ガイドライン

このドキュメントは、Claude Codeおよび人間の開発者が本プロジェクトで開発を行う際のルールとベストプラクティスを定義します。

## 開発前の必須手順

### 1. 仕様書の確認

**すべての開発タスクの前に、関連する仕様書を読むこと。**

#### 読むべき仕様書の優先順位:

1. **[GAME_SPECIFICATION.md](Specifications/GAME_SPECIFICATION.md)** - ゲーム全体仕様（最優先）
2. **[GAME_FLOW.md](Specifications/GAME_FLOW.md)** - シーン遷移、ゲームフロー
3. **[FOLDER_STRUCTURE.md](Specifications/FOLDER_STRUCTURE.md)** - フォルダ構成

#### タスク別の必読仕様:

- **バトルシステム修正** → GAME_SPECIFICATION.md の「バトルフェーズ詳細」
- **カード追加/修正** → GAME_SPECIFICATION.md の「カードシステム」
- **新シーン追加** → GAME_FLOW.md、FOLDER_STRUCTURE.md
- **UI変更** → GAME_SPECIFICATION.md の「UIシステム」
- **キャラクター追加** → GAME_SPECIFICATION.md の「キャラクターシステム」

### 2. 既存実装の確認

仕様書を読んだ後、関連する既存コードを確認：

```bash
# 例: カード関連の実装を確認
find Assets/Src/Card -name "*.cs"

# 例: UIコンポーネントを確認
find Assets/Src/UI -name "*.cs"
```

### 3. 実装計画の作成

複雑なタスクの場合、以下を明確にする：
- 変更するファイル
- 影響を受ける他のシステム
- テスト方法

## 開発中のルール

### コーディング規約

#### ファイル配置
- 新しいC#スクリプトは `Assets/Src/` の適切なサブフォルダに配置
- ScriptableObjectデータは `Assets/Data/` の適切なサブフォルダに配置
- UI画像は `Assets/Images/` に配置
- Prefabは `Assets/Prefabs/` に配置

#### 命名規則
- **クラス名:** PascalCase（例: `BattleStateMachine`）
- **メソッド名:** PascalCase（例: `ChangeState`）
- **フィールド名:** camelCase（例: `currentState`）
- **プライベートフィールド:** camelCase（例: `isActive`）
- **定数:** PascalCase（例: `MaxPlayers`）

#### コメント
- 日本語コメント推奨（プロジェクトの主言語）
- XML docコメント（`///`）でpublicメソッドを文書化
- 複雑なロジックには説明コメントを追加

#### 例:
```csharp
/// <summary>
/// バトルフェーズを変更します。
/// </summary>
/// <param name="newState">新しいフェーズ</param>
public void ChangeState(State newState)
{
    // 前フェーズの終了処理
    if (currentPhase != null)
    {
        currentPhase.Exit();
    }
    
    // 新フェーズの開始処理
    currentState = newState;
    currentPhase = GetPhase(newState);
    currentPhase?.Enter();
}
```

### Unity固有のルール

#### SerializeFieldの使用
- Inspectorで調整したい値は `[SerializeField]` を使用
- publicフィールドは避ける（カプセル化）

```csharp
[Header("バトル設定")]
[SerializeField] private int maxRounds = 3;
[Tooltip("1ラウンドあたりの制限時間（秒）")]
[SerializeField] private float timeLimit = 60f;
```

#### FindObjectOfTypeの使用
- Awake()で実行し、結果をキャッシュ
- 毎フレーム実行しない（パフォーマンス）

```csharp
private BattleStateMachine stateMachine;

void Awake()
{
    if (stateMachine == null)
    {
        stateMachine = FindObjectOfType<BattleStateMachine>();
    }
}
```

#### Coroutineの管理
- Coroutineの参照を保持
- 適切にStopCoroutine()を呼ぶ
- OnDestroy()でクリーンアップ

```csharp
private Coroutine currentRoutine;

public void StartAnimation()
{
    if (currentRoutine != null)
    {
        StopCoroutine(currentRoutine);
    }
    currentRoutine = StartCoroutine(AnimationRoutine());
}

void OnDestroy()
{
    if (currentRoutine != null)
    {
        StopCoroutine(currentRoutine);
    }
}
```

### 仕様との整合性

#### 新機能追加時
1. 仕様書に記載があるか確認
2. 記載がない場合、ユーザーに確認
3. 実装後、仕様書を更新

#### 既存機能修正時
1. 仕様書通りに動作しているか確認
2. 意図的な仕様変更か、バグ修正か明確にする
3. 仕様変更の場合、仕様書を更新

## 開発後の必須手順

### 1. 実装の自己レビュー

- [ ] 仕様書通りに実装されているか
- [ ] コーディング規約に従っているか
- [ ] 不要なコメントやデバッグコードが残っていないか
- [ ] 適切なフォルダに配置されているか

### 2. 仕様との照合

実装完了後、以下を確認：

```
実装した機能 vs 仕様書
↓
一致している → OK
一致していない → どちらを修正すべきかユーザーに確認
```

#### 照合チェックリスト:
- [ ] 機能の動作が仕様通りか
- [ ] パラメータ値が仕様通りか
- [ ] UI表示が仕様通りか
- [ ] エラーハンドリングが適切か

### 3. 仕様書の更新

実装によって仕様が変更された場合、仕様書を更新：

- **追加機能:** 該当セクションに追記
- **パラメータ変更:** 新しい値を記載
- **廃止機能:** 削除または非推奨マークを追加

### 4. テスト

以下を確認：
- [ ] コンパイルエラーがないか
- [ ] Unityエディタで実行できるか
- [ ] 基本動作が正常か
- [ ] 他の機能に悪影響がないか

## よくある問題と解決策

### Q: 仕様書とコードが矛盾している

**A:** 以下の手順で対処：
1. いつ矛盾が生じたか調査（git履歴など）
2. ユーザーに報告
3. どちらを正とするか確認
4. 修正と更新

### Q: 仕様書に記載がない機能を実装したい

**A:**
1. ユーザーに提案
2. 承認後、仕様書に追記
3. 実装
4. 最終確認

### Q: 複数の仕様書に同じ内容が書かれている

**A:**
- GAME_SPECIFICATION.md を最優先
- 他の仕様書は参照先として記載
- 重複を避け、リンクで参照

### Q: 古い仕様書が残っている

**A:**
- ファイル名に `[DEPRECATED]` プレフィックスを追加
- README.md から削除
- 必要なら新しい仕様書に移行

## Claude Code 専用ガイドライン

### タスク開始時のテンプレート

```markdown
## タスク: [タスク名]

### 1. 仕様確認
読むべき仕様書:
- [ ] GAME_SPECIFICATION.md
- [ ] [その他関連仕様]

関連セクション:
- [セクション名]

### 2. 実装計画
変更ファイル:
- [ファイルパス]

影響範囲:
- [影響を受けるシステム]

### 3. 実装
[実装内容]

### 4. 照合
- [ ] 仕様通りに実装完了
- [ ] 仕様書更新の必要性: [有/無]
```

### 自動チェック項目

実装完了時、以下を自動的に確認：
- [ ] Docs/Specifications/ 内の関連仕様を読んだか
- [ ] 仕様と実装が一致しているか
- [ ] 新機能の場合、仕様書を更新したか
- [ ] コーディング規約に従っているか
- [ ] 適切なフォルダに配置したか

## 緊急時の対応

### バグ修正の優先順位
1. **クリティカル:** ゲームが起動しない、クラッシュ → 即座に修正
2. **高:** ゲームが進行不能 → 24時間以内に修正
3. **中:** 機能が正常に動作しない → 1週間以内に修正
4. **低:** UI の見た目、軽微な問題 → 次回更新で修正

### 緊急修正時のルール
- 仕様確認は省略可（ただし事後確認必須）
- 修正後、必ず仕様との整合性を確認
- 緊急修正ログを CHANGELOG.md に記録

## まとめ

### 開発の基本フロー

```
1. タスク受領
   ↓
2. 仕様書を読む（Docs/Specifications/）
   ↓
3. 既存コードを確認
   ↓
4. 実装計画を作成
   ↓
5. 実装
   ↓
6. 仕様との照合
   ↓
7. 仕様書更新（必要な場合）
   ↓
8. テスト
   ↓
9. 完了報告
```

### 重要な心構え

1. **仕様書が真実** - 常に仕様書を基準にする
2. **確認してから実装** - 思い込みで実装しない
3. **変更は記録** - 仕様変更は必ず文書化
4. **整合性を保つ** - 仕様とコードの同期を維持

---

作成日: 2024-06-20
最終更新: 2024-06-20
