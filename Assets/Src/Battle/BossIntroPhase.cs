using System.Collections;
using UnityEngine;

/// <summary>
/// ボス登場演出フェーズ。
/// 画面上からボスが降ってきて、着地時に画面振動を発生させる。
/// 演出完了後、自動的にCardSelectフェーズへ遷移する。
/// </summary>
public class BossIntroPhase : MonoBehaviour, IBattlePhase
{
    [Header("演出設定")]
    [Tooltip("ボスが降ってくる開始Y座標（画面外上部）")]
    [SerializeField] private float startY = 8f;
    [Tooltip("ボスの着地Y座標")]
    [SerializeField] private float endY = 2f;
    [Tooltip("落下にかける時間（秒）")]
    [SerializeField] private float dropDuration = 0.8f;
    [Tooltip("落下のイージングカーブ")]
    [SerializeField] private AnimationCurve dropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("左右ぶれ設定")]
    [Tooltip("左右にぶれる幅")]
    [SerializeField] private float swayAmplitude = 0.8f;
    [Tooltip("ぶれの速度（周期）")]
    [SerializeField] private float swayFrequency = 8f;

    [Header("画面振動設定")]
    [Tooltip("振動の強さ")]
    [SerializeField] private float shakeIntensity = 0.5f;
    [Tooltip("振動時間（秒）")]
    [SerializeField] private float shakeDuration = 0.4f;
    [Tooltip("振動の減衰速度")]
    [SerializeField] private float shakeDecay = 3f;

    [Header("味方登場演出設定")]
    [Tooltip("味方の登場にかける時間（秒）")]
    [SerializeField] private float allyEnterDuration = 0.6f;
    [Tooltip("各味方の登場間隔（秒）")]
    [SerializeField] private float allyEnterStagger = 0.15f;

    [Header("HPゲージ演出設定")]
    [Tooltip("HPゲージが満タンになるまでの時間（秒）")]
    [SerializeField] private float hpFillDuration = 0.5f;
    [Tooltip("HPゲージ演出の開始遅延（キャラ登場完了からの遅延）")]
    [SerializeField] private float hpFillDelay = 0.1f;

    [Header("ゲームスタート演出設定")]
    [Tooltip("右から中央への移動時間（秒）")]
    [SerializeField] private float gameStartEnterDuration = 0.5f;
    [Tooltip("中央で待機する時間")]
    [SerializeField] private float gameStartHoldDuration = 0.6f;
    [Tooltip("中央から左への移動時間（秒）")]
    [SerializeField] private float gameStartExitDuration = 0.35f;
    [Tooltip("テキストの最大スケール")]
    [SerializeField] private float gameStartMaxScale = 12f;
    [Tooltip("テキストの色")]
    [SerializeField] private Color gameStartColor = new Color(1f, 0.9f, 0.2f, 1f);
    [Tooltip("テキストの縁取り色")]
    [SerializeField] private Color gameStartOutlineColor = new Color(0.8f, 0.2f, 0.1f, 1f);

    [Header("参照")]
    [Tooltip("BattleStateMachineへの参照（未設定の場合は自動探索）")]
    [SerializeField] private BattleStateMachine stateMachine;
    [Tooltip("ゲーム設定（フォント等を参照）")]
    [SerializeField] private GameTuning tuning;

    private bool isActive;
    private bool hasEntered;
    private Coroutine introCoroutine;
    private EnemyOnFieldController bossController;
    private Camera mainCamera;
    private Vector3 originalCameraPosition;
    private GameObject[] playerObjects;
    private Vector3[] playerOriginalPositions;
    private Vector3[] playerOriginalScales;
    private BattleFrameUI frameUI;

    public bool BlocksDebugAdvance => true;

    private void Awake()
    {
        if (stateMachine == null)
        {
            stateMachine = GetComponentInParent<BattleStateMachine>();
        }
        mainCamera = Camera.main;

        // フレームUIを生成
        CreateFrameUI();
    }

    /// <summary>
    /// フレームUIを生成
    /// </summary>
    private void CreateFrameUI()
    {
        frameUI = FindObjectOfType<BattleFrameUI>();
        if (frameUI == null)
        {
            GameObject frameUIObj = new GameObject("BattleFrameUI");
            frameUI = frameUIObj.AddComponent<BattleFrameUI>();
            Debug.Log("[BossIntroPhase] BattleFrameUI生成完了");
        }
    }

    public void Enter()
    {
        Debug.Log("[BossIntroPhase] Enter - ボス登場演出開始");
        isActive = true;
        hasEntered = true;

        // カメラの元位置を保存
        if (mainCamera != null)
        {
            originalCameraPosition = mainCamera.transform.position;
        }

        // 最初にすべてのキャラクターを画面外に配置
        HideAllCharactersInitially();

        // ボスを探すか生成する
        bossController = FindOrCreateBoss();

        // ボスも画面外に配置
        if (bossController != null)
        {
            bossController.transform.position = new Vector3(0f, startY, 0f);
        }

        // 演出開始
        introCoroutine = StartCoroutine(IntroSequence());
    }

    /// <summary>
    /// 演出開始前にすべてのキャラクターを画面外に配置
    /// </summary>
    private void HideAllCharactersInitially()
    {
        if (frameUI == null)
        {
            Debug.LogError("[BossIntroPhase] フレームUIが見つかりません");
            return;
        }

        // Playerタグを持つオブジェクトを取得して画面外に配置
        playerObjects = GameObject.FindGameObjectsWithTag("Player");

        if (playerObjects != null && playerObjects.Length > 0)
        {
            // 名前でソート
            System.Array.Sort(playerObjects, (a, b) => a.name.CompareTo(b.name));

            // プレイ人数に応じて味方枠レイアウトを決める
            // (ソロ=ボス下に1枠 / 2人以上=四隅、余り枠はグレーアウト)。配置より前に行う。
            frameUI.ConfigureForPlayerCount(playerObjects.Length);

            playerOriginalPositions = new Vector3[playerObjects.Length];
            playerOriginalScales = new Vector3[playerObjects.Length];

            // 画面外下部のY座標を計算
            float camHeight = mainCamera.orthographicSize * 2f;
            float offscreenY = mainCamera.transform.position.y - camHeight / 2f - 3f;

            for (int i = 0; i < playerObjects.Length && i < 4; i++)
            {
                // 元の位置とスケールを保存
                playerOriginalPositions[i] = playerObjects[i].transform.position;
                playerOriginalScales[i] = playerObjects[i].transform.localScale;

                // 四隅ポートレート配置(拡大＋上寄せ)。X はポートレート位置、Y は画面外から登場。
                Vector3 frameCenter = frameUI.GetAllyFrameCenter(i);
                var sr = playerObjects[i].GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    frameUI.GetAllyPortraitPlacement(i, sr.sprite, out Vector3 pPos, out float pScale);
                    playerObjects[i].transform.localScale = new Vector3(pScale, pScale, 1f);
                    playerObjects[i].transform.position = new Vector3(pPos.x, offscreenY, 0f);
                }
                else
                {
                    playerObjects[i].transform.position = new Vector3(frameCenter.x, offscreenY, 0f);
                }

                // 登場前から窓にクリップしておく → 画面外から上がってくる間、枠の外には出ない
                frameUI.ClipCharacterToFrame(i, playerObjects[i].transform);
            }

            Debug.Log($"[BossIntroPhase] {playerObjects.Length}人のプレイヤーを画面外に配置");
        }

        // Enemyタグを持つオブジェクトも画面外に配置
        try
        {
            var enemy = GameObject.FindGameObjectWithTag("Enemy");
            if (enemy != null)
            {
                // 画面外上部のY座標を計算
                float camHeight = mainCamera.orthographicSize * 2f;
                float offscreenTopY = mainCamera.transform.position.y + camHeight / 2f + 3f;

                Vector3 bossFrameCenter = frameUI.GetBossFrameCenter();
                enemy.transform.position = new Vector3(bossFrameCenter.x, offscreenTopY, 0f);
                Debug.Log("[BossIntroPhase] Enemyを画面外に配置");
            }
        }
        catch (UnityException)
        {
            // Enemyタグが定義されていない場合は無視
        }
    }

    public void Exit()
    {
        Debug.Log("[BossIntroPhase] Exit");
        isActive = false;

        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        // カメラ位置をリセット（Enter()が呼ばれた場合のみ）
        if (hasEntered && mainCamera != null)
        {
            mainCamera.transform.position = originalCameraPosition;
        }
    }

    public void Tick()
    {
        // コルーチンで処理するため、Tickでは特に何もしない
    }

    /// <summary>
    /// ボスを探すか、なければ生成する。スプライトは常にTest.pngから設定する。
    /// </summary>
    private EnemyOnFieldController FindOrCreateBoss()
    {
        // 既存のEnemyOnFieldControllerまたはEnemyタグのオブジェクトを探す
        EnemyOnFieldController controller = FindObjectOfType<EnemyOnFieldController>();
        GameObject bossObj = null;

        if (controller != null)
        {
            bossObj = controller.gameObject;
            Debug.Log($"[BossIntroPhase] 既存のEnemyOnFieldController発見: {bossObj.name}");
        }
        else
        {
            // Enemyタグを持つオブジェクトを探す
            try
            {
                bossObj = GameObject.FindGameObjectWithTag("Enemy");
                if (bossObj != null)
                {
                    Debug.Log($"[BossIntroPhase] Enemyタグのオブジェクト発見: {bossObj.name}");
                    controller = bossObj.GetComponent<EnemyOnFieldController>() ?? bossObj.AddComponent<EnemyOnFieldController>();
                }
            }
            catch (UnityException)
            {
                Debug.LogError("[BossIntroPhase] Enemyタグが定義されていません");
            }
        }

        // ボスオブジェクトが見つかった場合、スプライトを設定
        if (bossObj != null && controller != null)
        {
            var sr = bossObj.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"[BossIntroPhase] ボス({bossObj.name})にSpriteRendererがありません");
                return controller;
            }

            // スプライトをTest.pngから設定
            var bossSprite = LoadBossSprite();
            if (bossSprite != null)
            {
                Debug.Log($"[BossIntroPhase] 設定前のスプライト: {(sr.sprite != null ? sr.sprite.name : "null")}");
                Debug.Log($"[BossIntroPhase] 設定前のテクスチャ: {(sr.sprite != null && sr.sprite.texture != null ? sr.sprite.texture.name : "null")}");

                // EnemyOnFieldControllerのSetUpSpriteを使ってスプライトとスケールを設定
                controller.SetUpSprite(bossSprite);

                // sortingOrderを高くして確実に表示
                sr.sortingOrder = 100;

                Debug.Log($"[BossIntroPhase] 設定後のスプライト: {(sr.sprite != null ? sr.sprite.name : "null")}");
                Debug.Log($"[BossIntroPhase] 設定後のテクスチャ: {(sr.sprite != null && sr.sprite.texture != null ? sr.sprite.texture.name : "null")}");
                Debug.Log($"[BossIntroPhase] sortingOrder: {sr.sortingOrder}, scale: {bossObj.transform.localScale}");
                Debug.Log($"[BossIntroPhase] ボススプライト設定完了: {bossSprite.name}");
            }
            else
            {
                Debug.LogError("[BossIntroPhase] ボススプライトの読み込みに失敗しました");
            }
            return controller;
        }

        // EnemyOnFieldSpawnerがあればそれを使う
        var spawner = FindObjectOfType<EnemyOnFieldSpawner>();
        if (spawner != null && spawner.enemyOnFieldPrefab != null)
        {
            var go = Instantiate(spawner.enemyOnFieldPrefab);
            go.name = "Boss";
            var spawnerController = go.GetComponent<EnemyOnFieldController>() ?? go.AddComponent<EnemyOnFieldController>();

            // スプライトをTest.pngから設定
            var spawnerSr = go.GetComponent<SpriteRenderer>();
            if (spawnerSr == null)
            {
                Debug.LogError($"[BossIntroPhase] 生成したボス({go.name})にSpriteRendererがありません");
            }
            else
            {
                var bossSprite = LoadBossSprite();
                if (bossSprite != null)
                {
                    spawnerSr.sprite = bossSprite;
                    Debug.Log($"[BossIntroPhase] ボス生成＆スプライト設定: {go.name}, sprite={bossSprite.name}");
                }
                else
                {
                    Debug.LogError("[BossIntroPhase] ボススプライトの読み込みに失敗しました");
                }
            }
            return spawnerController;
        }

        // なければ仮のボスを生成
        Debug.LogWarning("[BossIntroPhase] EnemyOnFieldControllerが見つかりません。Test.pngからボスを生成します。");

        var bossGo = new GameObject("Boss", typeof(SpriteRenderer), typeof(EnemyOnFieldController));
        var bossSr = bossGo.GetComponent<SpriteRenderer>();
        bossSr.sortingOrder = 50;

        // Test.png（AllCharaスプライトシート）からボス用スプライトを読み込む
        // AllChara_0 を使用（最初のキャラクター）
        var loadedSprite = LoadBossSprite();
        if (loadedSprite != null)
        {
            bossSr.sprite = loadedSprite;
            Debug.Log($"[BossIntroPhase] ボススプライト設定: {loadedSprite.name}");
        }
        else
        {
            Debug.LogError("[BossIntroPhase] ボススプライトの読み込みに失敗しました");
        }

        return bossGo.GetComponent<EnemyOnFieldController>();
    }

    /// <summary>
    /// ボス用スプライトを現在のボス定義(EnemyData.bossSpriteName)に従って読み込む。
    /// </summary>
    private Sprite LoadBossSprite()
    {
        return BossSpriteResolver.Load(ResolveBossSpriteName());
    }

    /// <summary>
    /// 表示するボススプライト名を現在のボス定義(EnemyData.bossSpriteName)から解決する。
    /// RunProgress.CurrentBoss を優先し、無ければ EnemyRuntimeState.enemyData を見る。
    /// いずれも未設定なら警告を出して既定 AllChara_0 を使う。
    /// </summary>
    private string ResolveBossSpriteName()
    {
        EnemyData data = null;
        var run = FindObjectOfType<RunProgress>();
        if (run != null) data = run.CurrentBoss;
        if (data == null)
        {
            var state = FindObjectOfType<EnemyRuntimeState>();
            if (state != null) data = state.enemyData;
        }

        if (data != null && !string.IsNullOrEmpty(data.bossSpriteName))
            return data.bossSpriteName;

        Debug.LogWarning($"[BossIntroPhase] ボススプライト名が未設定です(boss={(data != null ? data.id : "null")})。" +
            $"EnemyData.bossSpriteName を設定してください。既定 AllChara_0 を使用します。");
        return "AllChara_0";
    }

    /// <summary>
    /// ボス登場演出シーケンス
    /// </summary>
    private IEnumerator IntroSequence()
    {
        // まず味方キャラクターを画面下から登場させる
        yield return StartCoroutine(AlliesEnterSequence());

        // 少し待つ
        yield return new WaitForSeconds(0.3f);

        if (bossController == null)
        {
            Debug.LogError("[BossIntroPhase] ボスが見つかりません");
            OnIntroComplete();
            yield break;
        }

        Transform bossTransform = bossController.transform;

        // フレームの位置を取得
        Vector3 bossFrameCenter = frameUI.GetBossFrameCenter();
        float camHeight = mainCamera.orthographicSize * 2f;
        float offscreenTopY = mainCamera.transform.position.y + camHeight / 2f + 3f;

        // 開始位置に配置（画面外上部）
        Vector3 startPos = new Vector3(bossFrameCenter.x, offscreenTopY, 0f);
        Vector3 endPos = bossFrameCenter;
        bossTransform.position = startPos;

        // ボスのスケールをフレームに合わせる
        var bossSr = bossController.GetComponent<SpriteRenderer>();
        if (bossSr != null && bossSr.sprite != null)
        {
            float bossScale = frameUI.CalculateBossScale(bossSr.sprite);
            bossTransform.localScale = new Vector3(bossScale, bossScale, 1f);
        }

        Debug.Log($"[BossIntroPhase] ボス落下開始: {startPos} → {endPos}");

        // 落下アニメーション（左右にぶれながら）
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);

            // イージングカーブを適用
            float curveT = dropCurve.Evaluate(t);

            // Y位置を補間
            float y = Mathf.Lerp(startPos.y, endPos.y, curveT);

            // 左右ぶれ（sin波、落下が進むにつれて減衰）
            float swayDecay = 1f - curveT; // 着地に近づくほど減衰
            float swayX = bossFrameCenter.x + Mathf.Sin(elapsed * swayFrequency) * swayAmplitude * swayDecay;

            bossTransform.position = new Vector3(swayX, y, 0f);

            yield return null;
        }

        // 最終位置に確定
        bossTransform.position = endPos;

        // ボスにも浮遊アニメーションを追加
        AddFloatingMotion(bossController.gameObject);

        Debug.Log("[BossIntroPhase] ボス着地 - 画面振動開始");

        // 着地時の画面振動
        yield return StartCoroutine(ScreenShake());

        // ゲームスタート演出
        yield return StartCoroutine(GameStartTextAnimation());

        // 少し待ってから次のフェーズへ
        yield return new WaitForSeconds(0.2f);

        OnIntroComplete();
    }

    /// <summary>
    /// 味方キャラクター登場シーケンス
    /// </summary>
    private IEnumerator AlliesEnterSequence()
    {
        // HideAllCharactersInitiallyで既に配置済み
        if (playerObjects == null || playerObjects.Length == 0)
        {
            Debug.LogWarning("[BossIntroPhase] Playerタグのオブジェクトが見つかりません");
            yield break;
        }

        int allyCount = Mathf.Min(playerObjects.Length, 4); // 最大4人
        Debug.Log($"[BossIntroPhase] 味方キャラクター {allyCount}人の登場演出開始");

        // 各キャラクターを順番に登場させる（コルーチンを並列実行）
        for (int i = 0; i < allyCount; i++)
        {
            StartCoroutine(EnterSingleAlly(playerObjects[i], i));
        }

        // 全員の登場完了を待つ
        float totalTime = allyEnterDuration + (allyCount - 1) * allyEnterStagger;
        yield return new WaitForSeconds(totalTime + 0.1f);

        Debug.Log("[BossIntroPhase] 味方キャラクター登場完了");

        // 全員登場後、共有HPゲージのアニメーションを開始
        yield return StartCoroutine(FillSharedHpGauge());
    }

    /// <summary>
    /// 共有HPゲージを0から満タンまでアニメーション
    /// </summary>
    private IEnumerator FillSharedHpGauge()
    {
        if (frameUI == null)
        {
            yield break;
        }

        // 少し遅延
        yield return new WaitForSeconds(hpFillDelay);

        // パーティクル発生間隔
        float particleInterval = 0.05f;
        float lastParticleTime = 0f;

        float elapsed = 0f;
        while (elapsed < hpFillDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hpFillDuration);

            // イージング（EaseOutQuad）
            float easeT = 1f - (1f - t) * (1f - t);
            frameUI.SetSharedHpGaugeFill(easeT);

            // パーティクルを一定間隔で発生
            if (elapsed - lastParticleTime >= particleInterval)
            {
                frameUI.EmitParticlesAtFillEdge(3);
                lastParticleTime = elapsed;
            }

            yield return null;
        }

        // 最終的に満タンに
        frameUI.SetSharedHpGaugeFill(1f);
        // 満タン時にパーティクルを大量に発生
        frameUI.PlayHpFillParticles();
        Debug.Log("[BossIntroPhase] 共有HPゲージ満タン");
    }

    /// <summary>
    /// 単体の味方キャラクターの登場アニメーション
    /// </summary>
    private IEnumerator EnterSingleAlly(GameObject ally, int index)
    {
        if (frameUI == null || index >= 4)
        {
            yield break;
        }

        // 登場タイミングをずらす
        yield return new WaitForSeconds(index * allyEnterStagger);

        // 四隅ポートレートの着地位置・スケールを算出(拡大＋上寄せ)
        Vector3 frameCenter = frameUI.GetAllyFrameCenter(index);
        float camHeight = mainCamera.orthographicSize * 2f;
        float offscreenY = mainCamera.transform.position.y - camHeight / 2f - 3f;

        Vector3 endPos = frameCenter;
        var sr = ally.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            frameUI.GetAllyPortraitPlacement(index, sr.sprite, out Vector3 pPos, out float pScale);
            endPos = pPos;
            ally.transform.localScale = new Vector3(pScale, pScale, 1f);
        }
        Vector3 startPos = new Vector3(endPos.x, offscreenY, 0f);

        float elapsed = 0f;
        while (elapsed < allyEnterDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / allyEnterDuration);

            // バウンス効果のあるイージング
            float easeT = EaseOutBack(t);
            ally.transform.position = Vector3.Lerp(startPos, endPos, easeT);

            yield return null;
        }

        ally.transform.position = endPos;

        // 枠の「窓」にクリップ(下端からキャラがはみ出さない窓表示)
        if (frameUI != null) frameUI.ClipCharacterToFrame(index, ally.transform);

        // 浮遊アニメーションを追加(待機の縦揺れは少し速め)
        AddFloatingMotion(ally, 1.0f);
    }

    /// <summary>
    /// キャラクターに浮遊アニメーションを追加
    /// </summary>
    private void AddFloatingMotion(GameObject character, float frequency = 0.5f)
    {
        // 既にFloatingCharacterMotionがあれば頻度だけ更新
        var existingMotion = character.GetComponent<UnderTheSpotlight.FloatingCharacterMotion>();
        if (existingMotion != null)
        {
            existingMotion.frequency = frequency;
            existingMotion.RebaseToCurrentPosition();
            return;
        }

        // FloatingCharacterMotionを追加
        var floating = character.AddComponent<UnderTheSpotlight.FloatingCharacterMotion>();
        floating.amplitude = 0.04f;       // 上下の揺れ幅（控えめ）
        floating.frequency = frequency;   // 縦揺れの速さ(大きいほど速い)
        floating.randomizePhase = true;   // キャラクターごとにずらす
        floating.enableRotation = false;  // 回転なし
    }

    /// <summary>
    /// EaseOutBackイージング関数（オーバーシュートあり）
    /// </summary>
    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>
    /// ゲームスタートテキストアニメーション
    /// 黒背景バナーと白文字で表示
    /// </summary>
    private IEnumerator GameStartTextAnimation()
    {
        if (mainCamera == null)
        {
            yield break;
        }

        // バナーコンテナ
        GameObject bannerObj = new GameObject("GameStartBanner");

        // 画面の左右端を取得
        Vector3 camPos = mainCamera.transform.position;
        float camWidth = mainCamera.orthographicSize * mainCamera.aspect * 2f;
        Vector3 centerPos = new Vector3(camPos.x, camPos.y, 0f);

        float bannerWidth = camWidth + 2f;  // 画面幅より少し広く
        float bannerHeight = 2.5f;

        bannerObj.transform.position = centerPos;

        // 黒背景を作成
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(bannerObj.transform);
        bgObj.transform.localPosition = Vector3.zero;

        var bgSr = bgObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = CreateSimpleSprite();
        bgSr.color = new Color(0f, 0f, 0f, 0.8f);  // 透明度80%の黒
        bgSr.sortingOrder = 990;

        // バナーサイズを設定
        float spriteSize = bgSr.sprite.bounds.size.x;
        bgObj.transform.localScale = new Vector3(bannerWidth / spriteSize, bannerHeight / spriteSize, 1f);

        // フォントの取得
        Font displayFont = null;
        if (tuning != null && tuning.displayFont != null)
        {
            displayFont = tuning.displayFont;
        }
        if (displayFont == null)
        {
            displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // GAME START! テキスト（白文字）
        GameObject textObj = new GameObject("GameStartText");
        textObj.transform.SetParent(bannerObj.transform);
        textObj.transform.localPosition = Vector3.zero;

        var textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = "GAME START!";
        textMesh.fontSize = 96;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        textMesh.font = displayFont;
        textMesh.characterSize = 0.15f;

        var textRenderer = textObj.GetComponent<MeshRenderer>();
        textRenderer.sortingOrder = 1000;

        // 登場アニメーション（縦に潰れた状態から展開）
        float enterDuration = 0.3f;
        float elapsed = 0f;
        Vector3 startScale = new Vector3(1f, 0f, 1f);
        Vector3 endScale = Vector3.one;

        bannerObj.transform.localScale = startScale;

        while (elapsed < enterDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / enterDuration);
            float easeT = EaseOutBackStrong(t);
            bannerObj.transform.localScale = Vector3.Lerp(startScale, endScale, easeT);
            yield return null;
        }
        bannerObj.transform.localScale = endScale;

        // 中央で待機
        yield return new WaitForSeconds(gameStartHoldDuration);

        // フェードアウト
        float exitDuration = 0.4f;
        elapsed = 0f;

        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / exitDuration);
            float alpha = 1f - t;

            // 背景のフェード
            Color bgColor = bgSr.color;
            bgSr.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0.8f * alpha);

            // テキストのフェード
            textMesh.color = new Color(1f, 1f, 1f, alpha);

            yield return null;
        }

        // バナーを削除
        Destroy(bannerObj);

        Debug.Log("[BossIntroPhase] ゲームスタート演出完了");
    }

    /// <summary>
    /// シンプルな白スプライトを生成
    /// </summary>
    private Sprite CreateSimpleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// EaseOutBackStrong - 強めのオーバーシュート
    /// </summary>
    private float EaseOutBackStrong(float t)
    {
        const float c1 = 2.5f;  // より強いオーバーシュート
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>
    /// EaseOutElastic - 弾力のあるバウンス効果
    /// </summary>
    private float EaseOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        const float c4 = (2f * Mathf.PI) / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    /// <summary>
    /// EaseInQuint - 5乗の急激な加速
    /// </summary>
    private float EaseInQuint(float t)
    {
        return t * t * t * t * t;
    }

    /// <summary>
    /// 画面振動エフェクト
    /// </summary>
    private IEnumerator ScreenShake()
    {
        if (mainCamera == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // 減衰を適用
            float decay = Mathf.Exp(-shakeDecay * elapsed);
            float intensity = shakeIntensity * decay;

            // ランダムなオフセット
            float offsetX = Random.Range(-intensity, intensity);
            float offsetY = Random.Range(-intensity, intensity);

            mainCamera.transform.position = originalCameraPosition + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        // 元の位置に戻す
        mainCamera.transform.position = originalCameraPosition;
    }

    /// <summary>
    /// 演出完了時の処理
    /// </summary>
    private void OnIntroComplete()
    {
        Debug.Log("[BossIntroPhase] ボス登場演出完了 - CardSelectへ遷移");

        if (stateMachine != null)
        {
            stateMachine.ChangeState(BattleStateMachine.State.CardSelect);
        }
    }
}
