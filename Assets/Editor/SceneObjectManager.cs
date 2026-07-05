using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// シーンにオブジェクトを配置・削除するためのエディタツール
/// </summary>
public class SceneObjectManager : EditorWindow
{
    [MenuItem("Tools/Scene Object Manager")]
    public static void ShowWindow()
    {
        GetWindow<SceneObjectManager>("Scene Object Manager");
    }

    private Vector2 scrollPos;

    // 遅延実行用フラグ
    private bool pendingInitialize = false;
    private System.Type pendingDeleteType = null;
    private string pendingDeleteName = null;

    private void OnGUI()
    {
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("シーンオブジェクト管理", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // シーンクリーンアップボタン
        DrawCleanupSection();

        GUILayout.Space(10);

        // HPバー (OnGUI)
        DrawObjectSection<SharedHpGaugeUI>("SharedHpGaugeUI", "HPバー (OnGUI)", CreateSharedHpGaugeUI);

        GUILayout.Space(10);

        // HPバー (SpriteRenderer)
        DrawObjectSection<SpriteHpGaugeUI>("SpriteHpGaugeUI", "HPバー (Sprite)", CreateSpriteHpGaugeUI);

        GUILayout.Space(10);

        // 弾幕フレーム
        DrawObjectSection<DanmakuFrameView>("DanmakuFrameView", "弾幕フレーム", CreateDanmakuFrameView);

        GUILayout.Space(10);

        // バトル背景
        DrawObjectSection<BattleBackgroundView>("BattleBackgroundView", "バトル背景", CreateBattleBackgroundView);

        GUILayout.Space(10);

        // BattleManager
        DrawBattleManagerSection();

        GUILayout.EndScrollView();
    }

    private void Update()
    {
        // 遅延実行処理
        if (pendingInitialize)
        {
            pendingInitialize = false;
            InitializeBattleScene();
            Repaint();
        }
    }

    private void DrawCleanupSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("シーンクリーンアップ", EditorStyles.boldLabel);

        if (GUILayout.Button("不要なオブジェクトを検出", GUILayout.Height(25)))
        {
            DetectUnnecessaryObjects();
        }

        GUILayout.Space(5);

        // Squareオブジェクト削除ボタン
        var squareObj = GameObject.Find("Square");
        if (squareObj != null)
        {
            var warningColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
            if (GUILayout.Button("Squareオブジェクトを削除", GUILayout.Height(25)))
            {
                DestroyImmediate(squareObj);
                MarkSceneDirty();
                Debug.Log("[SceneObjectManager] Squareオブジェクトを削除しました");
            }
            GUI.backgroundColor = warningColor;
        }

        GUILayout.Space(5);

        var origColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("バトルシーンを初期化", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("確認",
                "バトルシーンを初期化しますか？\n現在のオブジェクト配置はリセットされます。", "初期化", "キャンセル"))
            {
                pendingInitialize = true;
            }
        }
        GUI.backgroundColor = origColor;

        GUILayout.EndVertical();
    }

    private void DetectUnnecessaryObjects()
    {
        Debug.Log("=== 不要なオブジェクト検出 ===");

        // 全SpriteRendererを検出
        var spriteRenderers = FindObjectsOfType<SpriteRenderer>();
        foreach (var sr in spriteRenderers)
        {
            string path = GetGameObjectPath(sr.gameObject);
            Debug.Log($"SpriteRenderer: {path} (sortingOrder: {sr.sortingOrder}, sprite: {(sr.sprite != null ? sr.sprite.name : "null")})");
        }

        // Backgroundという名前のオブジェクトを検出
        var allObjects = FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name.ToLower().Contains("background") && obj.GetComponent<BattleBackgroundView>() == null)
            {
                Debug.LogWarning($"潜在的に不要なBackgroundオブジェクト: {GetGameObjectPath(obj)}");
            }
        }

        // Squareオブジェクトを検出
        var squareObj = GameObject.Find("Square");
        if (squareObj != null)
        {
            Debug.LogWarning($"Squareオブジェクトが存在します: {GetGameObjectPath(squareObj)}");
        }

        Debug.Log("=== 検出完了 ===");
    }

    /// <summary>
    /// プレイヤースプライト参照を修正
    /// </summary>
    [MenuItem("Tools/Fix Player Sprites")]
    public static void FixPlayerSprites()
    {
        // Player.pngのスプライトをロード
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Player.png");
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError("[FixPlayerSprites] Assets/Images/Player.png が見つかりません！");
            return;
        }

        // スプライトを抽出
        var spriteList = new System.Collections.Generic.List<Sprite>();
        foreach (var obj in sprites)
        {
            if (obj is Sprite sprite)
            {
                spriteList.Add(sprite);
                Debug.Log($"[FixPlayerSprites] スプライト発見: {sprite.name}");
            }
        }

        if (spriteList.Count == 0)
        {
            Debug.LogError("[FixPlayerSprites] Player.pngにスプライトが見つかりません！");
            return;
        }

        // Player1-4のSpriteRendererを探して修正
        for (int i = 1; i <= 4; i++)
        {
            var playerObj = GameObject.Find($"Player{i}");
            if (playerObj == null)
            {
                Debug.LogWarning($"[FixPlayerSprites] Player{i} が見つかりません");
                continue;
            }

            var sr = playerObj.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogWarning($"[FixPlayerSprites] Player{i} にSpriteRendererがありません");
                continue;
            }

            // スプライトを設定（Player_0, Player_1, Player_2, Player_3）
            string targetSpriteName = $"Player_{i - 1}";
            Sprite targetSprite = null;
            foreach (var sprite in spriteList)
            {
                if (sprite.name == targetSpriteName)
                {
                    targetSprite = sprite;
                    break;
                }
            }

            if (targetSprite == null)
            {
                // 見つからない場合はインデックスで試す
                int index = i - 1;
                if (index < spriteList.Count)
                {
                    targetSprite = spriteList[index];
                }
            }

            if (targetSprite != null)
            {
                sr.sprite = targetSprite;
                EditorUtility.SetDirty(sr);
                Debug.Log($"[FixPlayerSprites] Player{i} にスプライト {targetSprite.name} を設定しました");
            }
            else
            {
                Debug.LogWarning($"[FixPlayerSprites] Player{i} 用のスプライトが見つかりません");
            }
        }

        // シーンをダーティに
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);

        Debug.Log("[FixPlayerSprites] スプライト修正完了");
    }

    /// <summary>
    /// クイッククリーンアップ - 問題となるオブジェクトを即座に削除
    /// </summary>
    [MenuItem("Tools/Quick Cleanup Battle Scene")]
    public static void QuickCleanupBattleScene()
    {
        int deletedCount = 0;

        // 不要なルートレベルオブジェクトを削除
        string[] unnecessaryRootObjects = { "Square", "Player", "Visual" };
        foreach (var objName in unnecessaryRootObjects)
        {
            var obj = GameObject.Find(objName);
            // ルートレベルにあるもののみ削除（子オブジェクトは除外）
            if (obj != null && obj.transform.parent == null)
            {
                DestroyImmediate(obj);
                deletedCount++;
                Debug.Log($"[QuickCleanup] {objName}を削除しました");
            }
        }

        // BattleBackgroundSpriteを削除（ランタイム生成されたもの）
        var bgSprite = GameObject.Find("BattleBackgroundSprite");
        if (bgSprite != null)
        {
            DestroyImmediate(bgSprite);
            deletedCount++;
            Debug.Log("[QuickCleanup] BattleBackgroundSpriteを削除しました");
        }

        // SpriteHpGaugeUIを削除
        var spriteHpGauge = FindObjectOfType<SpriteHpGaugeUI>();
        if (spriteHpGauge != null)
        {
            DestroyImmediate(spriteHpGauge.gameObject);
            deletedCount++;
            Debug.Log("[QuickCleanup] SpriteHpGaugeUIを削除しました");
        }

        // BattleBackgroundViewをSpriteモードに変更（OnGUIはSpriteRendererより手前に描画されるため）
        var battleBg = FindObjectOfType<BattleBackgroundView>();
        if (battleBg != null && battleBg.useOnGUI)
        {
            battleBg.useOnGUI = false;
            EditorUtility.SetDirty(battleBg);
            Debug.Log("[QuickCleanup] BattleBackgroundViewをSpriteモードに変更しました");
        }

        // シーンをダーティに
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);

        Debug.Log($"[QuickCleanup] クリーンアップ完了: {deletedCount}個のオブジェクトを削除しました");
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private void InitializeBattleScene()
    {
        // 1. 不要なオブジェクトを削除
        var toDelete = new System.Collections.Generic.List<GameObject>();

        // SpriteHpGaugeUIを削除（OnGUI版を使用）
        var spriteHpGauge = FindObjectOfType<SpriteHpGaugeUI>();
        if (spriteHpGauge != null) toDelete.Add(spriteHpGauge.gameObject);

        // Squareオブジェクトを削除
        var squareObj = GameObject.Find("Square");
        if (squareObj != null) toDelete.Add(squareObj);

        // 単独のBackgroundオブジェクトを削除
        var allObjects = FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name == "Background" && obj.GetComponent<BattleBackgroundView>() == null)
            {
                // BattleManager配下でない場合のみ削除
                if (obj.transform.parent == null || obj.transform.parent.GetComponent<BattleStateMachine>() == null)
                {
                    toDelete.Add(obj);
                }
            }
        }

        foreach (var obj in toDelete)
        {
            Debug.Log($"削除: {obj.name}");
            DestroyImmediate(obj);
        }

        // 2. BattleManagerがなければ作成
        var battleManager = FindObjectOfType<BattleStateMachine>();
        if (battleManager == null)
        {
            CreateBattleManager();
        }

        // 3. BattleBackgroundViewをSpriteモードに設定（OnGUIはSpriteRendererより手前に描画されるため）
        var battleBg = FindObjectOfType<BattleBackgroundView>();
        if (battleBg != null)
        {
            battleBg.useOnGUI = false;
            EditorUtility.SetDirty(battleBg);
            Debug.Log("[SceneObjectManager] BattleBackgroundViewをSpriteモードに変更しました");
        }

        // 4. カメラ設定
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor = GameColorScheme.PrimaryDark;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 0, -10);
        }

        MarkSceneDirty();
        Debug.Log("バトルシーンを初期化しました");
    }

    private void DrawObjectSection<T>(string objectName, string displayName, System.Action createAction) where T : Component
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label($"{displayName} ({objectName})", EditorStyles.boldLabel);

        var obj = FindObjectOfType<T>();
        bool exists = obj != null;

        if (exists)
        {
            GUILayout.Label("シーンに存在します", EditorStyles.miniLabel);
            if (GUILayout.Button($"{displayName}を削除", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("確認",
                    $"{displayName}を削除しますか？", "削除", "キャンセル"))
                {
                    DestroyImmediate(obj.gameObject);
                    MarkSceneDirty();
                    Debug.Log($"[SceneObjectManager] {displayName}を削除しました");
                }
            }

            if (GUILayout.Button("選択", GUILayout.Height(25)))
            {
                Selection.activeGameObject = obj.gameObject;
                EditorGUIUtility.PingObject(obj.gameObject);
            }
        }
        else
        {
            GUILayout.Label("シーンに存在しません", EditorStyles.miniLabel);
            if (GUILayout.Button($"{displayName}を配置", GUILayout.Height(30)))
            {
                createAction?.Invoke();
                MarkSceneDirty();
                Debug.Log($"[SceneObjectManager] {displayName}を配置しました");
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawBattleManagerSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("BattleManager", EditorStyles.boldLabel);

        var battleManager = FindObjectOfType<BattleStateMachine>();
        bool exists = battleManager != null;

        if (exists)
        {
            GUILayout.Label("シーンに存在します", EditorStyles.miniLabel);

            if (GUILayout.Button("BattleManagerを選択", GUILayout.Height(25)))
            {
                Selection.activeGameObject = battleManager.gameObject;
                EditorGUIUtility.PingObject(battleManager.gameObject);
            }

            GUILayout.Space(5);
            GUILayout.Label("コンポーネント:", EditorStyles.miniLabel);

            var stateMachine = battleManager.GetComponent<BattleStateMachine>();
            var deckController = battleManager.GetComponent<PartyDeckController>();
            var enemyState = battleManager.GetComponent<EnemyRuntimeState>();
            var hpController = battleManager.GetComponent<SharedHpController>();
            var runProgress = battleManager.GetComponent<RunProgress>();

            DrawComponentStatus("BattleStateMachine", stateMachine != null);
            DrawComponentStatus("PartyDeckController", deckController != null);
            DrawComponentStatus("EnemyRuntimeState", enemyState != null);
            DrawComponentStatus("SharedHpController", hpController != null);
            DrawComponentStatus("RunProgress", runProgress != null);
        }
        else
        {
            GUILayout.Label("シーンに存在しません", EditorStyles.miniLabel);
            if (GUILayout.Button("BattleManagerを配置", GUILayout.Height(30)))
            {
                CreateBattleManager();
                MarkSceneDirty();
                Debug.Log("[SceneObjectManager] BattleManagerを配置しました");
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawComponentStatus(string name, bool exists)
    {
        string status = exists ? "[OK]" : "[--]";
        GUILayout.Label($"  {status} {name}", EditorStyles.miniLabel);
    }

    // === 作成メソッド ===

    private void CreateSharedHpGaugeUI()
    {
        var hpGaugeGo = new GameObject("SharedHpGaugeUI");
        var hpGaugeUI = hpGaugeGo.AddComponent<SharedHpGaugeUI>();

        // SharedHpControllerを探してアサイン
        var hpController = FindObjectOfType<SharedHpController>();
        if (hpController != null)
        {
            var sourceField = typeof(SharedHpGaugeUI).GetField("source",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (sourceField != null)
            {
                sourceField.SetValue(hpGaugeUI, hpController);
            }
        }

        Selection.activeGameObject = hpGaugeGo;
    }

    private void CreateSpriteHpGaugeUI()
    {
        var hpGaugeGo = new GameObject("SpriteHpGaugeUI");
        hpGaugeGo.transform.position = new Vector3(0, -7f, 0);

        var hpGaugeUI = hpGaugeGo.AddComponent<SpriteHpGaugeUI>();

        // SharedHpControllerを探してアサイン
        var hpController = FindObjectOfType<SharedHpController>();
        if (hpController != null)
        {
            var controllerField = typeof(SpriteHpGaugeUI).GetField("hpController",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (controllerField != null)
            {
                controllerField.SetValue(hpGaugeUI, hpController);
            }
        }

        Selection.activeGameObject = hpGaugeGo;
    }

    private void CreateDanmakuFrameView()
    {
        var frameGo = new GameObject("DanmakuFrame");
        frameGo.AddComponent<DanmakuFrameView>();
        Selection.activeGameObject = frameGo;
    }

    private void CreateBattleBackgroundView()
    {
        var bgGo = new GameObject("BattleBackground");
        bgGo.AddComponent<BattleBackgroundView>();
        bgGo.transform.SetAsFirstSibling();
        Selection.activeGameObject = bgGo;
    }

    private void CreateBattleManager()
    {
        var battleManager = new GameObject("BattleManager");

        // コンポーネントを追加
        var stateMachine = battleManager.AddComponent<BattleStateMachine>();
        stateMachine.initialState = BattleStateMachine.State.CardSelect;

        battleManager.AddComponent<BattleSceneController>();

        var deckController = battleManager.AddComponent<PartyDeckController>();
        deckController.playerCount = 4;
        deckController.drawCount = 3;

        battleManager.AddComponent<EnemyRuntimeState>();

        var hpController = battleManager.AddComponent<SharedHpController>();
        hpController.maxHp = 5;

        battleManager.AddComponent<RunProgress>();

        // フェーズコンテナを作成
        CreatePhasesContainer(battleManager);

        Selection.activeGameObject = battleManager;
    }

    private void CreatePhasesContainer(GameObject parent)
    {
        var phasesContainer = new GameObject("Phases");
        phasesContainer.transform.SetParent(parent.transform);

        // CardSelectPhase
        var cardSelectObj = new GameObject("CardSelectPhase");
        cardSelectObj.transform.SetParent(phasesContainer.transform);
        var cardSelectPhase = cardSelectObj.AddComponent<CardSelectPhase>();
        cardSelectPhase.playerCount = 4;
        cardSelectPhase.selectTimeLimit = 15f;

        // PartSelectPhase
        var partSelectObj = new GameObject("PartSelectPhase");
        partSelectObj.transform.SetParent(phasesContainer.transform);
        var partSelectPhase = partSelectObj.AddComponent<PartSelectPhase>();
        partSelectPhase.playerCount = 4;
        partSelectPhase.selectTimeLimit = 12f;

        // ActionCommandPhase
        var actionCommandObj = new GameObject("ActionCommandPhase");
        actionCommandObj.transform.SetParent(phasesContainer.transform);
        var actionCommandPhase = actionCommandObj.AddComponent<ActionCommandPhase>();
        actionCommandPhase.playerCount = 4;
        actionCommandPhase.roundsPerActionCommand = 3;

        // DodgePhase
        var dodgePhaseObj = new GameObject("DodgePhase");
        dodgePhaseObj.transform.SetParent(phasesContainer.transform);
        dodgePhaseObj.AddComponent<DodgePhase>();
    }

    private void MarkSceneDirty()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
    }
}
