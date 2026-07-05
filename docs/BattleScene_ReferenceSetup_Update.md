# バトルシーンセットアップスクリプト更新報告

## 更新日: 2026-06-20
## 最終更新: 2026-06-20 (Enemy/Visual修正)

## 問題点

セットアップスクリプトでオブジェクトは作成されていましたが、**コンポーネント間の参照が設定されていない**状態でした。

### 影響範囲

以下のコンポーネントが正しく動作しない可能性がありました：

- BattleStateMachine → フェーズへの参照がない
- CardSelectPhase → BattleStateMachine、PartyDeckController、PlayerTransformsへの参照がない
- PartSelectPhase → BattleStateMachine、CardSelectPhase、EnemyRuntimeState、DodgePhase、Enemyへの参照がない
- ActionCommandPhase → BattleStateMachine、CardSelectPhase、PartSelectPhase、EnemyRuntimeStateへの参照がない
- DodgePhase → BattleStateMachine、DanmakuFrameViewへの参照がない
- SharedHpGaugeUI → SharedHpControllerへの参照がない

---

## 解決策

### 実装内容

`SetupObjectReferences()` メソッドを新規追加し、SerializedObjectを使用して全ての参照を自動設定するようにしました。

#### 追加コード (BattleSceneSetup.cs:210-300)

```csharp
/// <summary>
/// SerializedObjectを使用してコンポーネント間の参照を自動設定する
/// </summary>
private static void SetupObjectReferences(
    BattleStateMachine stateMachine,
    CardSelectPhase cardSelectPhase,
    PartSelectPhase partSelectPhase,
    ActionCommandPhase actionCommandPhase,
    DodgePhase dodgePhase,
    PartyDeckController deckController,
    EnemyRuntimeState enemyState,
    SharedHpController hpController,
    GameObject enemy,
    SharedHpGaugeUI hpGaugeUI,
    DanmakuFrameView danmakuFrameView,
    GameObject playersContainer)
{
    // BattleStateMachineの参照設定
    SerializedObject stateMachineSO = new SerializedObject(stateMachine);
    stateMachineSO.FindProperty("cardSelectPhase").objectReferenceValue = cardSelectPhase;
    stateMachineSO.FindProperty("partSelectPhase").objectReferenceValue = partSelectPhase;
    stateMachineSO.FindProperty("actionCommandPhase").objectReferenceValue = actionCommandPhase;
    stateMachineSO.FindProperty("dodgePhase").objectReferenceValue = dodgePhase;
    stateMachineSO.ApplyModifiedProperties();

    // CardSelectPhaseの参照設定
    SerializedObject cardSelectSO = new SerializedObject(cardSelectPhase);
    cardSelectSO.FindProperty("stateMachine").objectReferenceValue = stateMachine;
    cardSelectSO.FindProperty("partyDeck").objectReferenceValue = deckController;

    // playerTransforms配列を設定
    Transform[] playerTransforms = new Transform[4];
    for (int i = 0; i < 4; i++)
    {
        playerTransforms[i] = playersContainer.transform.Find($"Player{i + 1}");
    }
    SerializedProperty playerTransformsProp = cardSelectSO.FindProperty("playerTransforms");
    playerTransformsProp.arraySize = 4;
    for (int i = 0; i < 4; i++)
    {
        playerTransformsProp.GetArrayElementAtIndex(i).objectReferenceValue = playerTransforms[i];
    }
    cardSelectSO.ApplyModifiedProperties();

    // (以下、PartSelectPhase、ActionCommandPhase、DodgePhase、SharedHpGaugeUIの設定)
    // ...
}
```

#### 呼び出し箇所 (BattleSceneSetup.cs:154-167)

セットアップスクリプトのメイン処理の最後に追加：

```csharp
// 7. オブジェクト参照の自動設定
SetupObjectReferences(
    stateMachine,
    cardSelectPhase,
    partSelectPhase,
    actionCommandPhase,
    dodgePhase,
    deckController,
    enemyState,
    hpController,
    enemy,
    hpGaugeUI,
    danmakuFrameView,
    playersContainer
);

Debug.Log("[BattleSceneSetup] オブジェクト参照設定完了");
```

---

## 設定される参照の一覧

### 1. BattleStateMachine
- `cardSelectPhase` → CardSelectPhase
- `partSelectPhase` → PartSelectPhase
- `actionCommandPhase` → ActionCommandPhase
- `dodgePhase` → DodgePhase

### 2. CardSelectPhase
- `stateMachine` → BattleStateMachine
- `partyDeck` → PartyDeckController
- `playerTransforms[0-3]` → Player1〜4のTransform

### 3. PartSelectPhase
- `stateMachine` → BattleStateMachine
- `cardSelectPhase` → CardSelectPhase
- `enemyState` → EnemyRuntimeState
- `dodgePhase` → DodgePhase
- `enemyTransform` → Enemy

### 4. ActionCommandPhase
- `stateMachine` → BattleStateMachine
- `cardSelectPhase` → CardSelectPhase
- `partSelectPhase` → PartSelectPhase
- `enemyState` → EnemyRuntimeState

### 5. DodgePhase
- `stateMachine` → BattleStateMachine
- `danmakuFrameView` → DanmakuFrameView

### 6. SharedHpGaugeUI
- `hpController` → SharedHpController

---

## 検証方法

セットアップスクリプト実行後、以下を確認してください：

### Unity Inspectorでの確認

1. BattleManagerを選択
2. BattleStateMachineコンポーネントを展開
3. 「フェーズコンポーネント」セクションで以下が設定されているか確認：
   - Card Select Phase
   - Part Select Phase
   - Action Command Phase
   - Dodge Phase

4. Phases/CardSelectPhaseを選択
5. CardSelectPhaseコンポーネントで以下が設定されているか確認：
   - State Machine
   - Party Deck
   - Player Transforms (Size: 4, 各要素にPlayer1〜4)

同様に、他のフェーズとUIコンポーネントも確認してください。

### コンソールログでの確認

セットアップ実行時に以下のログが表示されます：

```
[BattleSceneSetup] オブジェクト参照設定完了
[BattleSceneSetup] 以下の参照を自動設定しました:
  - BattleStateMachine → 全フェーズ (CardSelect, PartSelect, ActionCommand, Dodge)
  - CardSelectPhase → BattleStateMachine, PartyDeckController, PlayerTransforms[4]
  - PartSelectPhase → BattleStateMachine, CardSelectPhase, EnemyRuntimeState, DodgePhase, Enemy
  - ActionCommandPhase → BattleStateMachine, CardSelectPhase, PartSelectPhase, EnemyRuntimeState
  - DodgePhase → BattleStateMachine, DanmakuFrameView
  - SharedHpGaugeUI → SharedHpController
```

---

## 利点

### 1. 作業効率の向上
- 手動で20個以上の参照をドラッグ&ドロップする必要がなくなった
- セットアップが1クリックで完了

### 2. ヒューマンエラーの防止
- 参照の設定忘れがなくなった
- 間違ったオブジェクトを参照する心配がなくなった

### 3. 一貫性の保証
- 常に仕様書通りの構成が保証される
- チーム内で同じ環境を簡単に再現可能

---

## 今後の拡張性

このパターンを使えば、以下の拡張も容易です：

1. **GameTuningアセットの自動設定**
   - Resourcesフォルダから自動検索
   - 全フェーズに自動適用

2. **デフォルトアセットの自動設定**
   - DeckDefinitionのデフォルト作成
   - EnemyDataのデフォルト作成

3. **プレハブからの自動生成**
   - Playerプレハブからの生成
   - Enemyプレハブからの生成

---

## 追加修正2: キャラクタースプライト自動設定 (2026-06-20)

### 問題点

セットアップスクリプトがPlayer1〜4に仮の四角形スプライトを設定していましたが、既にPlayer.pngスプライトシートとCharacterDataアセットが存在していました。

### 解決策

以下の機能を追加しました：

1. **CharacterDataの自動読み込み**
   ```csharp
   CharacterData[] LoadAllCharacterData()
   {
       string[] guids = AssetDatabase.FindAssets("t:CharacterData",
           new[] { "Assets/Data/Characters" });
       // ...
   }
   ```

2. **GameSceneManagerからの選択パーティ取得**
   ```csharp
   CharacterData[] GetSelectedPartyOrDefault(CharacterData[] allCharacters)
   {
       var gameManager = Object.FindObjectOfType<GameSceneManager>();
       if (gameManager != null && gameManager.selectedParty != null)
       {
           // 選択済みパーティを使用
           return gameManager.selectedParty;
       }
       // デフォルト: 最初の4キャラクター
       return defaultParty;
   }
   ```

3. **プレイヤースプライトの自動設定**
   ```csharp
   for (int i = 0; i < 4; i++)
   {
       if (selectedParty != null && selectedParty[i] != null)
       {
           sr.sprite = selectedParty[i].characterSprite;
       }
   }
   ```

### 動作

- **CharacterSelectScene経由の場合**: 選択された4キャラクターのスプライトが設定される
- **直接BattleSceneを開く場合**: デフォルトで最初の4キャラクター（イルカ、ネコ、ヒツジ、ニシカワ）が設定される

---

## 追加修正: Enemy/Visual構造 (2026-06-20)

### 問題点

Enemyオブジェクトに以下の問題がありました：

1. Visual子オブジェクトにSpriteRendererが設定されていなかった
2. PartSelectPhaseがVisual子のSpriteRendererを参照するため、エラーが発生する可能性があった

### 解決策

```csharp
// Enemyルート: 回避フェーズで使用
var enemySr = enemy.AddComponent<SpriteRenderer>();
enemySr.sortingLayerName = "Default";
enemySr.sortingOrder = 5;
enemySr.sprite = CreateTempSprite(new Color(0.8f, 0.3f, 0.3f), 128, 128);

// Visual子: 部位選択フェーズで使用
GameObject enemyVisual = new GameObject("Visual");
enemyVisual.transform.SetParent(enemy.transform);
enemyVisual.transform.localPosition = Vector3.zero;

var visualSr = enemyVisual.AddComponent<SpriteRenderer>();
visualSr.sortingLayerName = "Default";
visualSr.sortingOrder = 6;  // Enemyルートより前面
visualSr.sprite = CreateTempSprite(new Color(0.9f, 0.4f, 0.4f), 128, 128);
```

### 用途

- **Enemyルート (SpriteRenderer)**: DodgePhaseで敵本体として表示
- **Visual子 (SpriteRenderer)**: PartSelectPhaseで部位ヒットボックスの親として使用

---

## 関連ドキュメント

- [BattleSceneSetup.cs](../Assets/Editor/BattleSceneSetup.cs) - セットアップスクリプト本体
- [BattleScene_Setup_Summary.md](BattleScene_Setup_Summary.md) - セットアップ完了報告
- [BattleScene_Verification_Checklist.md](BattleScene_Verification_Checklist.md) - 検証チェックリスト
- [GAME_SPECIFICATION.md](Specifications/GAME_SPECIFICATION.md) - ゲーム仕様書

---

制作: Under the Spotlight プロジェクト
更新日: 2026-06-20
