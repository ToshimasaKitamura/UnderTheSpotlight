using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// 回避フェーズの実装（仮）。
/// - Enter: 敵を上に移動、プレイヤー4人を左右端に小さく引っ込め、中央に黒いフィールドを表示するアニメーションを行う。
/// - EndDodge: 弾幕回避が終わったときに中央フィールドを消し、プレイヤーと敵を元に戻す。
/// 注意: 実プロジェクトのプレイヤー制御停止や入力管理は別途行ってください。
/// </summary>
public class DodgePhase : MonoBehaviour, IBattlePhase
{
    [Header("参照設定(Inspectorで割り当て可能)")]
    [Tooltip("敵をまとめた親Transform。未設定時は名前" + "Enemies" + "を探します")]
    [SerializeField] private Transform enemiesRoot;

    [Tooltip("中央に表示するフィールドの GameObject。未設定時はシーン内の名前" + "Field" + "を探します")]
    [SerializeField] private GameObject centralField;

    [Tooltip("プレイヤーの Transform 配列。未設定時はタグ'Player'で検索します（順序は不定）")]
    [SerializeField] private Transform[] playerTransforms = null;

    // デフォルトの左右ワールド位置（Inspectorで上書き可能）
    [Tooltip("左サイド用ワールド座標。配列は左側プレイヤーの順に指定してください。デフォルト値あり。")]
    public Vector3[] leftSideWorldPositions = new Vector3[] { new Vector3(-730f, -400f, 0f), new Vector3(-760f, -180f, 0f) };
    [Tooltip("右サイド用ワールド座標。配列は右側プレイヤーの順に指定してください。デフォルト値あり.")]
    public Vector3[] rightSideWorldPositions = new Vector3[] { new Vector3(730f, -400f, 0f), new Vector3(760f, -180f, 0f) };
    [Tooltip("左サイドから入る際の開始位置をターゲットからどれだけ離して始めるか(ワールド単位)")]
    public float leftSideStartDistanceWorld = 5f;
    [Tooltip("右サイドから入る際の開始位置をターゲットからどれだけ離して始めるか(ワールド単位)")]
    public float rightSideStartDistanceWorld = 5f;

    [Header("アニメーション設定")]
    public float enemyRiseDistance = 3f;
    public float enemyRiseDuration = 0.6f;
    public float playerMoveDuration = 0.6f;
    public float fieldScaleDuration = 0.4f;
    [Tooltip("中央フィールドが出現するときの表示スケール（ワールドスケール）。通常は大きな値を指定します。")]
    public float centralFieldAppearScale = 1000f;
    // 追加パラメータ: プレイヤーの出口/入り/復帰の時間と小さめスケール
    public float playerExitDuration = 0.4f; // 下に消える時間
    public float playerSideEntryDuration = 0.5f; // 端から入ってくる時間
    public float playerReturnDuration = 0.6f; // 下から元位置に戻る時間
    public float playerSmallScale = 0.5f; // 小さく表示する比率
    public float fieldRiseDistance = 3f; // フィールドが上へ戻るときの移動距離
    public float playerScaleUpDuration = 0.3f; // 画面外で元の大きさに戻す時間
    // フィールドのフェード時間（消えるときのフェード）
    public float fieldFadeOutDuration = 0.4f;
    [Header("EnemyOnField / Danmaku")]
    [Tooltip("EnemyOnField prefab to appear when enemies rise. If null, a placeholder will be created.")]
    public GameObject enemyOnFieldPrefab;
    [Tooltip("Spawn position for EnemyOnField (world units).")]
    public Vector3 enemyOnFieldSpawnPosition = new Vector3(0f, 256f, 0f);
    [Tooltip("EnemyOnField の出現開始位置の、スポーン位置からの上方向オフセット(ワールド単位)。")]
    public float enemyOnFieldEntryHeight = 600f;
    [Tooltip("Time it takes for EnemyOnField to appear from above to spawn position.")]
    public float enemyOnFieldAppearDuration = 0.6f;
    [Tooltip("Enemy が復帰時に、元位置のどれだけ上空から降りてくるか(ワールド単位)。スナップが見えないように画面外の値を設定。")]
    public float enemyReturnFromAboveHeight = 600f;
    [Tooltip("Reference to DanmakuManager which will handle bullets. If null, one will be created.")]
    public DanmakuManager danmakuManager;

    [Header("PlayerOnField (弾幕フィールド上の自機)")]
    [Tooltip("弾幕フィールド上に表示する自機 prefab(白い球)。1人分スポーンされる。")]
    public GameObject playerOnFieldPrefab;
    [Tooltip("自機の初期位置。中央フィールドの下端からの上方向オフセット(ワールド単位)。")]
    public float playerOnFieldBottomMargin = 100f;
    [Tooltip("各プレイヤー(P1..P4)のカラー。自機リング・UI 文字色に使われる。")]
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.3f, 0.3f, 1f),
        new Color(0.3f, 0.6f, 1f, 1f),
        new Color(0.3f, 1f, 0.3f, 1f),
        new Color(1f, 0.9f, 0.3f, 1f),
    };
    [Tooltip("1人のプレイヤーが操作する時間(秒)。経過後に自動で次のプレイヤーへ切替。")]
    public float playerTurnDuration = 4f;
    [Tooltip("全員で共有する最大HP。0 になるとゲームオーバー。")]
    public int sharedMaxHp = 5;
    [Tooltip("各プレイヤー切替時の UI セリフ。playerIndex に対応。空欄は自動文言。")]
    public string[] playerCallouts = new string[]
    {
        "1番さん出番ですッ！",
        "2番さん助けて〜！！",
        "3番さん任せたっ！",
        "4番さんお願いっ！",
    };

    [Header("依存(未設定は自動探索)")]
    [Tooltip("Dodge 終了で CardSelect に戻すための参照。")]
    [SerializeField] private BattleStateMachine stateMachine;
    [Tooltip("数値の唯一の真実(Excel/CSV連動)。設定で下記既定値を上書き。")]
    [SerializeField] private GameTuning tuning;
    [Tooltip("援護ボム管理。未設定時はシーンから探索。")]
    [SerializeField] private BombController bombController;

    // runtime instances
    private GameObject enemyOnFieldInstance;
    private EnemyOnFieldController enemyOnFieldController;
    private BossDanmakuPattern bossDanmakuPattern;
    private BossMovementPattern bossMovementPattern;
    private ActivePlayerRing activePlayerRing;
    private DebugMultiPlayerInput debugInput;
    private PlayerTurnManager turnManager;
    private PlayerSwitchAnnouncer announcer;
    private SharedHpController sharedHp;
    private SharedHpGaugeUI hpGauge;
    private List<GameObject> playerOnFieldInstances = new List<GameObject>();
    private readonly List<PlayerOnFieldHittable> subscribedHittables = new List<PlayerOnFieldHittable>();

    // 内部状態の保存
    private bool isActive = false;
    private Vector3 enemiesOriginalPos;
    private Vector3 centralFieldOriginalPos;
    private Vector3 centralFieldOriginalScale;
    // プレイヤーの元位置・スケールを格納する配列（playerTransforms と同じ長さ）
    private Vector3[] playersOriginalPosArr = null;
    private Vector3[] playersOriginalScaleArr = null;

    // enemiesRoot が未設定の場合、個別の敵オブジェクトを集めて移動するためのリスト
    private List<Transform> individualEnemies = null;
    private List<Vector3> individualEnemiesOriginalPos = null;

    private Coroutine currentRoutine;

    // Set alpha of centralField (handles SpriteRenderer, UI Image, and generic Renderers with _Color)
    private void SetFieldAlpha(float a)
    {
        if (centralField == null) return;

        // SpriteRenderers
        var srs = centralField.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (sr == null) continue;
            var c = sr.color;
            c.a = a;
            sr.color = c;
        }

        // UI Images
        var imgs = centralField.GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            if (img == null) continue;
            var c = img.color;
            c.a = a;
            img.color = c;
        }

        // Generic renderers (meshes, sprites using materials)
        var rends = centralField.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            if (r == null) continue;
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = a;
                    mat.color = c;
                }
            }
        }
    }

    // カメラ変換で使う安全な Z 値を計算するヘルパー
    private float GetSafeScreenZ(Camera cam, Vector3 worldPos)
    {
        if (cam == null) return 10f;
        // カメラからワールド位置への前方距離（カメラの forward 方向への射影）を使う
        float z = Vector3.Dot(worldPos - cam.transform.position, cam.transform.forward);
        if (z <= 0.01f)
        {
            // 近いか負の値の場合はカメラからの距離を使う（最低値を設定）
            z = Vector3.Distance(cam.transform.position, worldPos);
            if (z <= 0.01f) z = 10f;
        }
        return z;
    }

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (bombController == null) bombController = FindObjectOfType<BombController>();
        ApplyTuning();
    }

    private void ApplyTuning()
    {
        if (tuning == null) return;
        playerTurnDuration = tuning.playerTurnDuration;
        sharedMaxHp = tuning.maxHp;
        var cols = tuning.ResolvePlayerColors();
        if (cols != null && cols.Length > 0) playerColors = cols;
        if (tuning.playerCallouts != null && tuning.playerCallouts.Length > 0)
            playerCallouts = tuning.playerCallouts;
    }

    void Start()
    {
        // デフォルトでは無効化
        enabled = false;
    }

    // Inspector でデフォルト値を確実に出すための処理
    private void OnValidate()
    {
        // Ensure arrays have sensible defaults so Inspector doesn't show empty
        if (leftSideWorldPositions == null || leftSideWorldPositions.Length == 0)
        {
            leftSideWorldPositions = new Vector3[] { new Vector3(-730f, -400f, 0f), new Vector3(-760f, -180f, 0f) };
        }
        if (rightSideWorldPositions == null || rightSideWorldPositions.Length == 0)
        {
            rightSideWorldPositions = new Vector3[] { new Vector3(730f, -400f, 0f), new Vector3(760f, -180f, 0f) };
        }
        if (leftSideStartDistanceWorld <= 0f) leftSideStartDistanceWorld = 5f;
        if (rightSideStartDistanceWorld <= 0f) rightSideStartDistanceWorld = 5f;
    }

    void Update()
    {
        // フェーズ中の継続処理が必要ならここに
    }

    /// <summary>
    /// フェーズ開始: アニメーションを開始する
    /// </summary>
    public void Enter()
    {
        enabled = true;
        UnityEngine.Debug.Log("[Battle] フェーズ遷移: 回避フェーズに入ります");

        // 参照がなければ自動探索
        if (enemiesRoot == null)
        {
            var go = GameObject.Find("Enemies");
            if (go != null) enemiesRoot = go.transform;
        }

        if (centralField == null)
        {
            var go = GameObject.Find("Field");
            if (go != null) centralField = go;
        }

        if (playerTransforms == null || playerTransforms.Length == 0)
        {
            var gos = GameObject.FindGameObjectsWithTag("Player");
            if (gos != null && gos.Length > 0)
            {
                playerTransforms = new Transform[gos.Length];
                for (int i = 0; i < gos.Length; i++) playerTransforms[i] = gos[i].transform;
            }
        }

        // 保存: 敵の元位置
        individualEnemies = null;
        individualEnemiesOriginalPos = null;
        if (enemiesRoot != null)
        {
            enemiesOriginalPos = enemiesRoot.position;
        }
        else
        {
            // enemiesRoot が見つからなければタグ 'Enemy' で個別取得して移動対象とする
            var enemyGOs = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemyGOs != null && enemyGOs.Length > 0)
            {
                individualEnemies = new List<Transform>(enemyGOs.Length);
                individualEnemiesOriginalPos = new List<Vector3>(enemyGOs.Length);
                foreach (var eg in enemyGOs)
                {
                    individualEnemies.Add(eg.transform);
                    individualEnemiesOriginalPos.Add(eg.transform.position);
                }
                UnityEngine.Debug.Log($"[DodgePhase] 個別の敵 {individualEnemies.Count} 件を移動対象として取得しました");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[DodgePhase] enemiesRoot が未設定で、タグ 'Enemy' のオブジェクトも見つかりませんでした。敵は移動しません。");
            }
        }

        // 保存: プレイヤーの元位置・スケール（playerTransforms と同じ長さで格納）
        if (playerTransforms != null && playerTransforms.Length > 0)
        {
            int n = playerTransforms.Length;
            playersOriginalPosArr = new Vector3[n];
            playersOriginalScaleArr = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var t = playerTransforms[i];
                if (t != null)
                {
                    playersOriginalPosArr[i] = t.position;
                    playersOriginalScaleArr[i] = t.localScale;
                }
                else
                {
                    playersOriginalPosArr[i] = Vector3.zero;
                    playersOriginalScaleArr[i] = Vector3.one;
                }
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("[DodgePhase] playerTransforms が未設定または空です。プレイヤー移動は行いません。");
        }

        // 中央フィールドを初期非表示 (scale=0) + 弾幕クリッピング境界を確定
        if (centralField != null)
        {
            // 保存してから非表示スケールにする
            centralFieldOriginalPos = centralField.transform.position;
            centralFieldOriginalScale = centralField.transform.localScale;
            centralField.SetActive(true);

            // フル表示時の bounds を捕捉して DanmakuManager に渡す(弾はこの範囲内のみ描画)
            if (danmakuManager == null) danmakuManager = FindObjectOfType<DanmakuManager>();
            var fieldSr = centralField.GetComponentInChildren<SpriteRenderer>();
            if (danmakuManager != null && fieldSr != null)
            {
                danmakuManager.fieldBounds = fieldSr.bounds;
                danmakuManager.useFieldBounds = true;
            }

            centralField.transform.localScale = Vector3.zero;
        }

        // 実際の遷移処理をコルーチンで行う
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(EnterSequence());
    }

    /// <summary>
    /// フェーズ終了: ここでは自動復帰を行わず EndDodge() を呼ぶのを想定
    /// </summary>
    public void Exit()
    {
        // フェーズを強制終了する場合の挙動:
        // 既に EnterSequence が走っている場合はそれを止めて復帰シーケンスを開始する。
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        // まだ EnterSequence によって isActive が立っているなら、復帰シーケンスを実行して
        // プレイヤー/敵を元に戻す。そうでなければ単純に無効化する。
        if (isActive)
        {
            currentRoutine = StartCoroutine(EndDodgeSequence());
        }
        else
        {
            enabled = false;
        }

        UnityEngine.Debug.Log("[Battle] フェーズ遷移: 回避フェーズを終了します");
    }

    public void Tick()
    {
        TickBombInput();
    }

    // ボム入力: 数字 1〜4 をその場で押下(wasPressedThisFrame)し、
    // かつ「現在の操作プレイヤーではない」場合、そのプレイヤーのボムを発動。
    private void TickBombInput()
    {
        if (!isActive) return;
        if (bombController == null || danmakuManager == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        int active = turnManager != null ? turnManager.CurrentActivePlayer : -1;
        TryBomb(kb.digit1Key, 0, active);
        TryBomb(kb.digit2Key, 1, active);
        TryBomb(kb.digit3Key, 2, active);
        TryBomb(kb.digit4Key, 3, active);
    }

    private void TryBomb(KeyControl key, int playerIndex, int activePlayer)
    {
        if (key == null || !key.wasPressedThisFrame) return;
        if (playerIndex == activePlayer) return; // 操作中プレイヤーは移動入力なのでボム使用不可
        if (!bombController.TryUseBomb(playerIndex)) return;
        // 効果: 画面上の全弾を削除 + 簡易フラッシュ
        danmakuManager.ClearAllBullets();
        StartCoroutine(BombFlashRoutine(playerIndex));
        UnityEngine.Debug.Log($"[DodgePhase] P{playerIndex + 1} ボム発動");
    }

    private IEnumerator BombFlashRoutine(int playerIndex)
    {
        var col = (playerColors != null && playerIndex < playerColors.Length)
            ? playerColors[playerIndex] : Color.white;
        var go = new GameObject("BombFlash", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = SolidWhiteSprite();
        sr.color = new Color(col.r, col.g, col.b, 0.6f);
        sr.sortingLayerID = SortingLayer.NameToID("Field");
        sr.sortingOrder = 100;
        go.transform.position = Vector3.zero;
        go.transform.localScale = new Vector3(20000f, 20000f, 1f);

        float t = 0f, dur = 0.35f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Clamp01(t / dur);
            var c = sr.color; c.a = 0.6f * p; sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    private static Sprite cachedBombFlashSprite;
    private static Sprite SolidWhiteSprite()
    {
        if (cachedBombFlashSprite != null) return cachedBombFlashSprite;
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        cachedBombFlashSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
        return cachedBombFlashSprite;
    }

    // 回避フェーズは HP0/1周で自前終了するため、デバッグ Enter の誤遷移を抑止
    public bool BlocksDebugAdvance => true;

    /// <summary>
    /// 弾幕回避が終わったときに呼ぶ。中央フィールドを消して元に戻す一連の処理を行う。
    /// </summary>
    public void EndDodge()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        UnityEngine.Debug.Log("[DodgePhase] EndDodge 呼び出し: 復帰シーケンスを開始します");
        currentRoutine = StartCoroutine(EndDodgeSequence());
    }

    // Convenience: spawn player UI circles (for local testing)
    public void SpawnPlayerUI(Color col, Vector3 worldPos, string id)
    {
        // Create a simple UI Canvas-based circle at world position
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas not found for player UI");
            return;
        }
        var go = new GameObject($"PlayerUI_{id}", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(PlayerUICircle));
        go.transform.SetParent(canvas.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(50f, 50f);
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        rect.position = screenPos;
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = col;
        var pic = go.GetComponent<PlayerUICircle>();
        pic.Init(col, id);
    }

    private IEnumerator SpawnAndEnterEnemyOnField()
    {
        // 「敵が大きい」タイミングでセリフを言わせる:
        // ここで先に persistent Enemy(シーン常駐、Visual scale=1024 のでかいキャラ) に対して
        // 予告セリフを表示する。降下用の enemyOnFieldInstance は scale=1 で小さいので NG。
        var enemyStateForSpeech = FindObjectOfType<EnemyRuntimeState>();
        yield return ShowEnemyTelegraphSpeech(enemyStateForSpeech, 2.5f);

        if (enemyOnFieldInstance != null)
        {
            Destroy(enemyOnFieldInstance);
            enemyOnFieldInstance = null;
            enemyOnFieldController = null;
        }

        Vector3 entryStartPos = enemyOnFieldSpawnPosition + Vector3.up * enemyOnFieldEntryHeight;

        if (enemyOnFieldPrefab != null)
        {
            enemyOnFieldInstance = Instantiate(enemyOnFieldPrefab, entryStartPos, Quaternion.identity);
        }
        else
        {
            enemyOnFieldInstance = new GameObject("EnemyOnField_Auto", typeof(SpriteRenderer), typeof(EnemyOnFieldController));
            enemyOnFieldInstance.transform.position = entryStartPos;
            enemyOnFieldInstance.GetComponent<SpriteRenderer>().sprite = CreateWhitePlaceholderSprite();
        }

        enemyOnFieldController = enemyOnFieldInstance.GetComponent<EnemyOnFieldController>();
        if (enemyOnFieldController == null) enemyOnFieldController = enemyOnFieldInstance.AddComponent<EnemyOnFieldController>();

        Sprite sprite = FindFirstSpriteInChildren(enemyOnFieldInstance);
        if (sprite == null)
        {
            var rootSr = enemyOnFieldInstance.GetComponent<SpriteRenderer>();
            if (rootSr == null) rootSr = enemyOnFieldInstance.AddComponent<SpriteRenderer>();
            sprite = CreateWhitePlaceholderSprite();
            rootSr.sprite = sprite;
            rootSr.sortingOrder = 100;
        }

        enemyOnFieldController.SetUpSprite(sprite);
        enemyOnFieldInstance.transform.localScale = Vector3.one;

        Vector3 start = entryStartPos;
        Vector3 end = enemyOnFieldSpawnPosition;
        float t = 0f;
        while (t < enemyOnFieldAppearDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / enemyOnFieldAppearDuration);
            enemyOnFieldInstance.transform.position = Vector3.Lerp(start, end, p);
            yield return null;
        }
        enemyOnFieldInstance.transform.position = end;

        if (danmakuManager == null) danmakuManager = FindObjectOfType<DanmakuManager>();
        if (danmakuManager == null)
        {
            UnityEngine.Debug.LogWarning("[DodgePhase] DanmakuManager がシーンに存在しません。弾幕は撃たれません。");
            yield break;
        }

        // セリフは降下前に表示済み。ここからは弾幕パターン+移動の起動だけ。
        var enemyState = FindObjectOfType<EnemyRuntimeState>();

        var pattern = enemyOnFieldInstance.GetComponent<BossDanmakuPattern>();
        if (pattern == null) pattern = enemyOnFieldInstance.AddComponent<BossDanmakuPattern>();
        pattern.danmakuManager = danmakuManager;
        pattern.StartPattern();
        bossDanmakuPattern = pattern;

        var movement = enemyOnFieldInstance.GetComponent<BossMovementPattern>();
        if (movement == null) movement = enemyOnFieldInstance.AddComponent<BossMovementPattern>();
        bossMovementPattern = movement;
        if (enemyState != null && enemyState.HasActiveEffect(PartDestructionEffect.DisableMovement))
        {
            UnityEngine.Debug.Log("[DodgePhase] DisableMovement 発動中: ボス移動を停止");
        }
        else
        {
            movement.StartPattern();
        }
    }

    /// <summary>
    /// 弾幕開始直前に、敵がそのターンに「予告した」セリフを吹き出しで一定秒数表示する。
    /// 部位破壊によってこの予告通りの攻撃を弱化/無効化できる、という設計の核。
    /// </summary>
    private IEnumerator ShowEnemyTelegraphSpeech(EnemyRuntimeState state, float showSeconds)
    {
        if (state == null || enemyOnFieldInstance == null) yield break;
        string line = state.GetTelegraphSpeech();
        if (string.IsNullOrWhiteSpace(line)) yield break;

        // 吹き出しを画面上端中央に配置するモーダル(MVP)。将来は敵頭上にトラッキング配置に拡張可。
        var canvasGo = new GameObject("DodgeIntroSpeechCanvas",
            typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var bubble = SpeechBubble.Create(canvasGo.transform, "Telegraph");
        var rt = bubble.RectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -80f);
        rt.sizeDelta = new Vector2(900f, 260f);
        bubble.Text = line;

        // 軽いポップイン
        float t = 0f;
        rt.localScale = Vector3.zero;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.2f);
            rt.localScale = Vector3.one * (0.7f + 0.3f * Mathf.Sin(p * Mathf.PI * 0.5f));
            yield return null;
        }
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(showSeconds);
        if (canvasGo != null) Destroy(canvasGo);
    }

    private static Sprite CreateWhitePlaceholderSprite()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        var cols = new Color32[size * size];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }

    // Native オブジェクトのアクセス例外に備えて try/catch で防御。
    private static Sprite FindFirstSpriteInChildren(GameObject root)
    {
        var rends = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var rend in rends)
        {
            if (rend == null) continue;
            try
            {
                if (rend.sprite != null) return rend.sprite;
            }
            catch (System.Exception)
            {
                continue;
            }
        }
        return null;
    }

    // EnemyOnField を上方向に移動させて破棄するシーケンス（SpawnAndEnterEnemyOnField の逆）
    private IEnumerator ExitEnemyOnFieldUpward()
    {
        if (enemyOnFieldInstance == null) yield break;

        Vector3 start = enemyOnFieldInstance.transform.position;
        Vector3 end = start + Vector3.up * enemyOnFieldEntryHeight;
        float t = 0f;
        while (t < enemyOnFieldAppearDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / enemyOnFieldAppearDuration);
            enemyOnFieldInstance.transform.position = Vector3.Lerp(start, end, p);
            yield return null;
        }

        Destroy(enemyOnFieldInstance);
        enemyOnFieldInstance = null;
        enemyOnFieldController = null;
    }

    // 敵を上に上げ、プレイヤーを左右に引っ込め、中央フィールドを表示するシーケンス
    private IEnumerator EnterSequence()
    {
        isActive = true;

        // 1) 敵を上に移動（root があれば root を動かす。なければ個別の敵を上に移動）
        float t = 0f;
        while (t < enemyRiseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / enemyRiseDuration);

            if (enemiesRoot != null)
            {
                Vector3 enemyTarget = enemiesOriginalPos + Vector3.up * enemyRiseDistance;
                enemiesRoot.position = Vector3.Lerp(enemiesOriginalPos, enemyTarget, p);
            }
            else if (individualEnemies != null)
            {
                for (int i = 0; i < individualEnemies.Count; i++)
                {
                    var ent = individualEnemies[i];
                    if (ent == null) continue;
                    Vector3 from = individualEnemiesOriginalPos[i];
                    Vector3 to = from + Vector3.up * enemyRiseDistance;
                    ent.position = Vector3.Lerp(from, to, p);
                }
            }

            // プレイヤー移動は敵が上がり終わってから行うためここでは行わない
            yield return null;
        }

        // 確実に最終値にする
        if (enemiesRoot != null)
        {
            enemiesRoot.position = enemiesOriginalPos + Vector3.up * enemyRiseDistance;
        }
        else if (individualEnemies != null)
        {
            for (int i = 0; i < individualEnemies.Count; i++)
            {
                var ent = individualEnemies[i];
                if (ent == null) continue;
                ent.position = individualEnemiesOriginalPos[i] + Vector3.up * enemyRiseDistance;
            }
        }

        // 敵が上がり終わった瞬間にプレイヤーを下へ消し、小さくしてから端から入れる
        // 1a) 下に消して小さくする
        yield return StartCoroutine(PlayersExitDownAndShrink());
        // 1b) 端から小さく入ってくる
        yield return StartCoroutine(PlayersEnterFromSides());

        // ここで EnemyOnField を上から出す（Enemy が上がった直後）
        yield return StartCoroutine(SpawnAndEnterEnemyOnField());

        // 2) 中央フィールドをスケールアップ（指定の大きさで出す）
        if (centralField != null)
        {
            t = 0f;
            var fieldTrans = centralField.transform;
            Vector3 targetScale = Vector3.one * centralFieldAppearScale;
            // Ensure alpha is full when appearing
            SetFieldAlpha(1f);
            while (t < fieldScaleDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / fieldScaleDuration);
                fieldTrans.localScale = Vector3.Lerp(Vector3.zero, targetScale, p);
                yield return null;
            }
            fieldTrans.localScale = targetScale;
        }

        // 弾幕フィールド上に自機(白い球)を表示
        SpawnPlayersOnField();

        UnityEngine.Debug.Log("[DodgePhase] EnterSequence 完了");
        currentRoutine = null;
    }

    private void SpawnPlayersOnField()
    {
        DestroyPlayersOnField();
        if (playerOnFieldPrefab == null)
        {
            UnityEngine.Debug.LogWarning("[DodgePhase] playerOnFieldPrefab が未設定のため自機(白い球)は表示されません。");
            return;
        }

        Bounds fieldBounds = GetCentralFieldWorldBounds(out bool hasBounds);
        Vector3 spawnPos = hasBounds
            ? new Vector3(fieldBounds.center.x, fieldBounds.min.y + playerOnFieldBottomMargin, 0f)
            : Vector3.zero;

        var go = Instantiate(playerOnFieldPrefab, spawnPos, Quaternion.identity);
        go.name = "PlayerOnField";

        // デバッグ用マルチプレイヤー入力を attach (turnSource は後で設定)。
        // PlayerOnFieldController.Awake が GetComponent<IPlayerOnFieldInput>() で参照を取得する。
        debugInput = go.GetComponent<DebugMultiPlayerInput>();
        if (debugInput == null) debugInput = go.AddComponent<DebugMultiPlayerInput>();

        var controller = go.GetComponent<PlayerOnFieldController>();
        if (controller == null) controller = go.AddComponent<PlayerOnFieldController>();
        controller.movementBounds = fieldBounds;
        controller.enableBounds = hasBounds;

        var hittable = go.GetComponent<PlayerOnFieldHittable>();
        if (hittable == null) hittable = go.AddComponent<PlayerOnFieldHittable>();
        hittable.Hit += HandlePlayerHit;
        subscribedHittables.Add(hittable);

        playerOnFieldInstances.Add(go);

        // 永続 HP コントローラに購読(被弾→TakeDamage、HP0→ゲームオーバー)
        ResolveSharedHp();

        SpawnActivePlayerRing(go.transform);
        SpawnAnnouncer();
        StartTurnRotation();
    }

    private void ResolveSharedHp()
    {
        if (sharedHp == null) sharedHp = FindObjectOfType<SharedHpController>();
        if (hpGauge == null) hpGauge = FindObjectOfType<SharedHpGaugeUI>();
        if (sharedHp != null)
        {
            sharedHp.OnDepleted -= HandleHpDepleted;
            sharedHp.OnDepleted += HandleHpDepleted;
        }
    }

    private void HandleHpDepleted()
    {
        UnityEngine.Debug.Log("[DodgePhase] HP 0 — ゲームオーバー(強制終了)");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SpawnActivePlayerRing(Transform target)
    {
        if (activePlayerRing != null) return;
        var ringObj = new GameObject("ActivePlayerRing");
        activePlayerRing = ringObj.AddComponent<ActivePlayerRing>();
        activePlayerRing.playerColors = playerColors;
        activePlayerRing.target = target;
    }

    private void SpawnAnnouncer()
    {
        if (announcer != null) return;
        var anObj = new GameObject("PlayerSwitchAnnouncer");
        announcer = anObj.AddComponent<PlayerSwitchAnnouncer>();
        announcer.playerColors = playerColors;
        if (playerCallouts != null && playerCallouts.Length > 0) announcer.callouts = playerCallouts;
    }

    private void StartTurnRotation()
    {
        if (turnManager != null) return;
        var tmObj = new GameObject("PlayerTurnManager");
        turnManager = tmObj.AddComponent<PlayerTurnManager>();
        turnManager.playerCount = (playerColors != null && playerColors.Length > 0) ? playerColors.Length : 4;
        turnManager.turnDuration = playerTurnDuration;
        turnManager.ActivePlayerChanged += HandleActivePlayerChanged;
        turnManager.RoundCompleted += HandleRoundCompleted;

        if (debugInput != null) debugInput.turnSource = turnManager;

        turnManager.StartTurns(0);
    }

    private void HandleRoundCompleted()
    {
        UnityEngine.Debug.Log("[DodgePhase] 全プレイヤー1周完了。弾幕モード終了。");
        EndDodge();
    }

    private void HandleActivePlayerChanged(int newIndex)
    {
        if (activePlayerRing != null) activePlayerRing.SetActivePlayer(newIndex);
        if (announcer != null) announcer.Announce(newIndex);
    }

    private void HandlePlayerHit(PlayerOnFieldHittable target)
    {
        UnityEngine.Debug.Log("[DodgePhase] 自機被弾。HP -1。");
        if (sharedHp != null) sharedHp.TakeDamage(1);
        // ※ ヒッタブルは無敵時間後に再被弾可能なので Hit 購読は維持する。
    }

    private void UnsubscribeAllHittables()
    {
        for (int i = 0; i < subscribedHittables.Count; i++)
        {
            if (subscribedHittables[i] != null) subscribedHittables[i].Hit -= HandlePlayerHit;
        }
        subscribedHittables.Clear();
    }

    private Bounds GetCentralFieldWorldBounds(out bool hasBounds)
    {
        hasBounds = false;
        if (centralField == null) return default;
        var sr = centralField.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return default;
        hasBounds = true;
        return sr.bounds;
    }

    private void DestroyPlayersOnField()
    {
        UnsubscribeAllHittables();

        if (turnManager != null)
        {
            turnManager.StopTurns();
            turnManager.ActivePlayerChanged -= HandleActivePlayerChanged;
            turnManager.RoundCompleted -= HandleRoundCompleted;
            Destroy(turnManager.gameObject);
            turnManager = null;
        }

        if (announcer != null)
        {
            Destroy(announcer.gameObject);
            announcer = null;
        }

        // 永続オブジェクトなので破棄しない。購読解除のみ。参照は null に戻して次回 Enter で再解決。
        if (sharedHp != null)
        {
            sharedHp.OnDepleted -= HandleHpDepleted;
            sharedHp = null;
        }
        hpGauge = null;

        if (debugInput != null)
        {
            debugInput.turnSource = null;
            debugInput = null;
        }

        if (activePlayerRing != null)
        {
            Destroy(activePlayerRing.gameObject);
            activePlayerRing = null;
        }

        for (int i = 0; i < playerOnFieldInstances.Count; i++)
        {
            if (playerOnFieldInstances[i] != null) Destroy(playerOnFieldInstances[i]);
        }
        playerOnFieldInstances.Clear();
    }

    // プレイヤーを下に消して縮小するコルーチン
    private IEnumerator PlayersExitDownAndShrink()
    {
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        Camera cam = Camera.main;
        float t = 0f;
        while (t < playerExitDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / playerExitDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                // まずは下に移動（ビューポート y を負にする）
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(playersOriginalPosArr[i]);
                    vp.y = Mathf.Lerp(vp.y, -0.2f, p);
                    vp.z = GetSafeScreenZ(cam, playersOriginalPosArr[i]);
                    Vector3 world = cam.ViewportToWorldPoint(vp);
                    world.z = playersOriginalPosArr[i].z;
                    tr.position = world;
                }
                else
                {
                    tr.position = Vector3.Lerp(playersOriginalPosArr[i], playersOriginalPosArr[i] + Vector3.down * 4f, p);
                }

                // 縮小
                tr.localScale = Vector3.Lerp(playersOriginalScaleArr[i], playersOriginalScaleArr[i] * playerSmallScale, p);
            }
            yield return null;
        }

        // 最終位置をオフスクリーン下に確定
        if (cam != null)
        {
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                Vector3 vp = cam.WorldToViewportPoint(playersOriginalPosArr[i]);
                vp.y = -0.2f;
                vp.x = vp.x; // keep original x for the moment
                vp.z = GetSafeScreenZ(cam, playersOriginalPosArr[i]);
                Vector3 world = cam.ViewportToWorldPoint(vp);
                world.z = playersOriginalPosArr[i].z;
                tr.position = world;
                tr.localScale = playersOriginalScaleArr[i] * playerSmallScale;
            }
        }
        else
        {
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                tr.position = playersOriginalPosArr[i] + Vector3.down * 4f;
                tr.localScale = playersOriginalScaleArr[i] * playerSmallScale;
            }
        }
        yield break;
    }

    // 現在位置（サイド表示）のまま下に消えるコルーチン（小さいまま）
    private IEnumerator PlayersExitFromCurrentToBelow()
    {
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        Camera cam = Camera.main;
        float t = 0f;
        // 現在の位置からビューポート下へ移動
        while (t < playerExitDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / playerExitDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                if (cam != null)
                {
                    Vector3 curVp = cam.WorldToViewportPoint(tr.position);
                    curVp.y = Mathf.Lerp(curVp.y, -0.2f, p);
                    Vector3 world = cam.ViewportToWorldPoint(curVp);
                    world.z = playersOriginalPosArr[i].z;
                    tr.position = world;
                }
                else
                {
                    tr.position = Vector3.Lerp(tr.position, playersOriginalPosArr[i] + Vector3.down * 4f, p);
                }
                // 小さいまま維持
                tr.localScale = Vector3.Lerp(tr.localScale, playersOriginalScaleArr[i] * playerSmallScale, p);
            }
            yield return null;
        }
        yield break;
    }

    // 下でスケールを戻しながら下->元位置へ移動してくるコルーチン
    private IEnumerator PlayersRestoreFromBelowToOriginal()
    {
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        Camera cam = Camera.main;

        // まず各プレイヤーの現在位置（下）を取得し、復帰開始位置として扱う
        Vector3[] startPos = new Vector3[playerTransforms.Length];
        Vector3[] startScale = new Vector3[playerTransforms.Length];
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            var tr = playerTransforms[i];
            if (tr == null)
            {
                startPos[i] = playersOriginalPosArr[i];
                startScale[i] = playersOriginalScaleArr[i];
            }
            else
            {
                startPos[i] = tr.position;
                startScale[i] = tr.localScale;
            }
        }

        // 1) 画面外（下）で元の大きさに戻す（スケールのみ）
        if (playerScaleUpDuration > 0f)
        {
            float ts = 0f;
            while (ts < playerScaleUpDuration)
            {
                ts += Time.deltaTime;
                float ps = Mathf.SmoothStep(0f, 1f, ts / playerScaleUpDuration);
                for (int i = 0; i < playerTransforms.Length; i++)
                {
                    var tr = playerTransforms[i];
                    if (tr == null) continue;
                    tr.localScale = Vector3.Lerp(startScale[i], playersOriginalScaleArr[i], ps);
                    // position stays at startPos (off-screen)
                    tr.position = startPos[i];
                }
                yield return null;
            }
            // 確実にフルサイズに
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                tr.localScale = playersOriginalScaleArr[i];
                tr.position = startPos[i];
            }
        }

        // 2) 下から元の位置へ移動してくる（サイズは既に元に戻っている）
        float tm = 0f;
        while (tm < playerReturnDuration)
        {
            tm += Time.deltaTime;
            float pm = Mathf.SmoothStep(0f, 1f, tm / playerReturnDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                tr.position = Vector3.Lerp(startPos[i], playersOriginalPosArr[i], pm);
                tr.localScale = Vector3.Lerp(playersOriginalScaleArr[i], playersOriginalScaleArr[i], pm); // keep full
            }
            yield return null;
        }

        // 最終位置に確定
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            var tr = playerTransforms[i];
            if (tr == null) continue;
            tr.position = playersOriginalPosArr[i];
            tr.localScale = playersOriginalScaleArr[i];
        }

        yield break;
    }

    // プレイヤーを画面端（左右）から入ってくるように移動させるコルーチン
    private IEnumerator PlayersEnterFromSides()
    {
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        Camera cam = Camera.main;

        // 目標位置（画面端の小さい状態）を決める
        Vector3[] targetPositions = new Vector3[playerTransforms.Length];
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            int side = (i < 2) ? -1 : 1;
            // プレイヤー同士が被らないように少しずらす（同じサイド内でYをずらす）
            float[] sideYOffsets = new float[] { 0.18f, 0.28f }; // index 0->下寄り, 1->やや上
            int indexInSide = (i < 2) ? i : (i - 2);
            float targetVpY = sideYOffsets[Mathf.Clamp(indexInSide, 0, sideYOffsets.Length - 1)];

            if (cam != null)
            {
                // 優先順序:
                // 1) 左/右のワールド座標が Inspector で指定されていればそれを使う
                // 2) 左のスクリーン座標が指定されていればそれを使う
                // 3) デフォルトのビューポート位置を使う
                if (side < 0 && leftSideWorldPositions != null && indexInSide < leftSideWorldPositions.Length && leftSideWorldPositions[indexInSide] != Vector3.zero)
                {
                    targetPositions[i] = leftSideWorldPositions[indexInSide];
                    // preserve z
                    targetPositions[i].z = playersOriginalPosArr[i].z;
                }
                else if (side > 0 && rightSideWorldPositions != null && (indexInSide < rightSideWorldPositions.Length) && rightSideWorldPositions[indexInSide] != Vector3.zero)
                {
                    targetPositions[i] = rightSideWorldPositions[indexInSide];
                    targetPositions[i].z = playersOriginalPosArr[i].z;
                }
                else
                {
                    float targetVpX = (side < 0) ? 0.12f : 0.88f;
                    Vector3 vp = new Vector3(targetVpX, targetVpY, cam.WorldToViewportPoint(playersOriginalPosArr[i]).z);
                    Vector3 world = cam.ViewportToWorldPoint(vp);
                    world.z = playersOriginalPosArr[i].z;
                    targetPositions[i] = world;
                }
            }
            else
            {
                // ワールド座標のオフセットで代替。左右でYも少しずらす
                Vector3 offset = new Vector3(side * 6f, (indexInSide == 0) ? -1.8f : -1.2f, 0f);
                targetPositions[i] = playersOriginalPosArr[i] + offset;
            }
        }

        // 出発点（画面外の左右）を決めて、そこから targetPositions へ移動
        Vector3[] startPositions = new Vector3[playerTransforms.Length];
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            int side = (i < 2) ? -1 : 1;
            // 同じサイド内のインデックス
            int indexInSide = (i < 2) ? i : (i - 2);
            float[] sideYOffsets = new float[] { 0.18f, 0.28f };
            float targetVpY = sideYOffsets[Mathf.Clamp(indexInSide, 0, sideYOffsets.Length - 1)];

            if (cam != null)
            {
                // Start positions should be off-screen horizontally and move horizontally to the target.
                // Use the computed targetPositions[i] (which already honors world overrides) as the target.
                Vector3 targetWorld = targetPositions[i];
                // Determine horizontal start offset in world units
                float startDist = (side < 0) ? leftSideStartDistanceWorld : rightSideStartDistanceWorld;
                Vector3 startWorld = targetWorld;
                if (side < 0)
                {
                    // left: start further left
                    startWorld.x = targetWorld.x - startDist;
                }
                else
                {
                    // right: start further right
                    startWorld.x = targetWorld.x + startDist;
                }
                // keep same Y and Z as target (Z aligned to player original Z)
                startWorld.y = targetWorld.y;
                startWorld.z = playersOriginalPosArr[i].z;
                // Safety: if start is too close to target (practically same), push further out so it clearly comes from side
                if (Mathf.Abs(startWorld.x - targetWorld.x) < 0.01f)
                {
                    float push = (side < 0) ? -10f : 10f;
                    startWorld.x = targetWorld.x + push;
                }
                startPositions[i] = startWorld;
                UnityEngine.Debug.Log($"[DodgePhase] PlayersEnterFromSides idx={i} side={side} start={startPositions[i]} target={targetPositions[i]} startDist={startDist}");
            }
            else
            {
                // Yは少しずらしてスタート
                startPositions[i] = playersOriginalPosArr[i] + new Vector3(side * 8f, (indexInSide == 0) ? -1.8f : -1.2f, 0f);
            }

            // 先に startPositions に置いておく
            var tr = playerTransforms[i];
            if (tr != null) tr.position = startPositions[i];
        }

        float t = 0f;
        while (t < playerSideEntryDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / playerSideEntryDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                tr.position = Vector3.Lerp(startPositions[i], targetPositions[i], p);
                // 小さいまま維持
                tr.localScale = Vector3.Lerp(playersOriginalScaleArr[i] * playerSmallScale, playersOriginalScaleArr[i] * playerSmallScale, p);
            }
            yield return null;
        }

        // 最終的に targetPositions に確定
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            var tr = playerTransforms[i];
            if (tr == null) continue;
            tr.position = targetPositions[i];
            tr.localScale = playersOriginalScaleArr[i] * playerSmallScale;
        }

        yield break;
    }

    // 中央フィールドを消して元に戻すシーケンス
    private IEnumerator EndDodgeSequence()
    {
        UnityEngine.Debug.Log("[DodgePhase] EndDodgeSequence 開始");

        // 0a) ボス挙動(弾幕+移動)を停止 + 残弾を一掃 + クリッピング解除
        if (bossDanmakuPattern != null)
        {
            bossDanmakuPattern.StopPattern();
            bossDanmakuPattern = null;
        }
        if (bossMovementPattern != null)
        {
            bossMovementPattern.StopPattern();
            bossMovementPattern = null;
        }
        if (danmakuManager != null)
        {
            danmakuManager.ClearAllBullets();
            danmakuManager.useFieldBounds = false;
        }

        // 0b) 自機(白い球)を破棄
        DestroyPlayersOnField();

        // 1) EnemyOnField を上に戻して破棄
        yield return StartCoroutine(ExitEnemyOnFieldUpward());

        // 2) 中央フィールドを縮小して消す
        if (centralField != null)
        {
            // フィールドをその場でフェードアウトしてから上へ移動または非表示
            var fieldTrans = centralField.transform;
            // まずフェードアウト（alpha を 1->0）
            float tf = 0f;
            while (tf < fieldFadeOutDuration)
            {
                tf += Time.deltaTime;
                float pf = Mathf.SmoothStep(0f, 1f, tf / fieldFadeOutDuration);
                SetFieldAlpha(1f - pf);
                yield return null;
            }
            SetFieldAlpha(0f);
            // 位置を上へ移動して非表示
            float t = 0f;
            Vector3 start = fieldTrans.position;
            Vector3 end = centralFieldOriginalPos + Vector3.up * fieldRiseDistance;
            while (t < fieldScaleDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / fieldScaleDuration);
                fieldTrans.position = Vector3.Lerp(start, end, p);
                yield return null;
            }
            fieldTrans.position = end;
            // restore scale & deactivate
            fieldTrans.localScale = centralFieldOriginalScale;
            centralField.SetActive(false);
            UnityEngine.Debug.Log("[DodgePhase] 中央フィールドをフェードアウトして上へ戻しました");
        }

        // 2) プレイヤーはまず下に消える（現在の小さいまま下へ）
        yield return StartCoroutine(PlayersExitFromCurrentToBelow());

        // 3) プレイヤーを下で元の大きさに戻しつつ下->元位置へ移動して復帰
        yield return StartCoroutine(PlayersRestoreFromBelowToOriginal());

        UnityEngine.Debug.Log("[DodgePhase] プレイヤー復帰開始");

        // 3) 敵を上空にスナップしてから元位置に降下させる
        Vector3 aboveOffset = Vector3.up * enemyReturnFromAboveHeight;
        if (enemiesRoot != null)
        {
            Vector3 from = enemiesOriginalPos + aboveOffset;
            Vector3 to = enemiesOriginalPos;
            enemiesRoot.position = from;
            float t2 = 0f;
            while (t2 < enemyRiseDuration)
            {
                t2 += Time.deltaTime;
                float p2 = Mathf.SmoothStep(0f, 1f, t2 / enemyRiseDuration);
                enemiesRoot.position = Vector3.Lerp(from, to, p2);
                yield return null;
            }
            enemiesRoot.position = to;
        }
        else if (individualEnemies != null)
        {
            int m = Mathf.Min(individualEnemies.Count, individualEnemiesOriginalPos.Count);
            Vector3[] startPos = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                startPos[i] = individualEnemiesOriginalPos[i] + aboveOffset;
                if (individualEnemies[i] != null) individualEnemies[i].position = startPos[i];
            }

            float t2 = 0f;
            while (t2 < enemyRiseDuration)
            {
                t2 += Time.deltaTime;
                float p2 = Mathf.SmoothStep(0f, 1f, t2 / enemyRiseDuration);
                for (int i = 0; i < m; i++)
                {
                    var ent = individualEnemies[i];
                    if (ent == null) continue;
                    ent.position = Vector3.Lerp(startPos[i], individualEnemiesOriginalPos[i], p2);
                }
                yield return null;
            }
            for (int i = 0; i < m; i++)
            {
                var ent = individualEnemies[i];
                if (ent == null) continue;
                ent.position = individualEnemiesOriginalPos[i];
            }
        }

        UnityEngine.Debug.Log("[DodgePhase] 敵復帰完了");
        UnityEngine.Debug.Log("[DodgePhase] EndDodgeSequence 完了: 元の状態に復帰しました");
        isActive = false;
        currentRoutine = null;

        // ボス1体撃破扱い: RunProgress に通知し、連戦中なら次のボスへ(=CardSelect)、ステージクリアなら WorldMap へ。
        var run = FindObjectOfType<RunProgress>();
        if (run != null && run.currentStage != null)
        {
            int beforeIdx = run.currentBossIndex;
            run.OnBossDefeated();
            bool stageDone = run.currentStage == null || run.currentBossIndex >= run.currentStage.BossCount;
            // ※OnBossDefeated 内で全撃破時は accumulatedExp 等が増え、必要なら clearedStageIds に追加されるが、
            //   stage の参照自体は EndStage を呼ぶまで残るので、上の条件で判定する。
            if (stageDone)
            {
                UnityEngine.Debug.Log($"[DodgePhase] ステージクリア → WorldMap へ (exp={run.accumulatedExp}, bombs={run.accumulatedBombs})");
                if (stateMachine != null) stateMachine.ChangeState(BattleStateMachine.State.WorldMap);
                yield break;
            }
            else
            {
                UnityEngine.Debug.Log($"[DodgePhase] 次のボスへ {beforeIdx + 1}/{run.currentStage.BossCount}");
                // 次のボスを EnemyRuntimeState に差し替え
                var enemyState = FindObjectOfType<EnemyRuntimeState>();
                var nextBoss = run.CurrentBoss;
                if (enemyState != null && nextBoss != null)
                {
                    enemyState.enemyData = nextBoss;
                    enemyState.ResetAll();
                }
            }
        }

        // 連戦継続 or RunProgress 無し時のフォールバック: CardSelect に戻ってループ
        if (stateMachine != null)
        {
            stateMachine.ChangeState(BattleStateMachine.State.CardSelect);
        }
    }

    // プレイヤーを左右の端に移動させ、縮小する。p は 0..1 の進捗。
    private void MovePlayersTowardsSides(float p)
    {
        if (playerTransforms == null || playersOriginalPosArr == null || playersOriginalScaleArr == null) return;
        int n = Mathf.Min(playerTransforms.Length, playersOriginalPosArr.Length);
        for (int i = 0; i < n; i++)
        {
            var t = playerTransforms[i];
            if (t == null) continue;

            // 4人想定: index 0..3 -> 左左右右 に割り当て
            int side = (i < 2) ? -1 : 1; // -1: left, 1: right

            // 画面端方向へオフセット（ワールド座標でおおよそ）
            Vector3 offset = new Vector3(side * 6f, -2f, 0f); // 下に引っ込めつつ左右へ
            Vector3 targetPos = playersOriginalPosArr[i] + offset;

            t.position = Vector3.Lerp(playersOriginalPosArr[i], targetPos, p);

            // スケールを小さくする
            Vector3 targetScale = playersOriginalScaleArr[i] * 0.5f;
            t.localScale = Vector3.Lerp(playersOriginalScaleArr[i], targetScale, p);
        }
    }

    // プレイヤーを元に戻す。p は 0..1 で 1 が完全復帰
    private void RestorePlayers(float p)
    {
        if (playerTransforms == null || playersOriginalPosArr == null) return;
        int n = Mathf.Min(playerTransforms.Length, playersOriginalPosArr.Length);
        for (int i = 0; i < n; i++)
        {
            var t = playerTransforms[i];
            if (t == null) continue;
            t.position = Vector3.Lerp(t.position, playersOriginalPosArr[i], p);
            t.localScale = Vector3.Lerp(t.localScale, playersOriginalScaleArr[i], p);
        }
    }
}
