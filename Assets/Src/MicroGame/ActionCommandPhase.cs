using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アクションコマンド(ミニゲーム)フェーズ。
/// 流れ: CardSelect の選択を読み修飾値構築 → ランダム選出して MiniGameStart 表示 →
/// 全員が順に決定 → ラウンドを難化させながら実行 → 集計 → Dodge へ。
///
/// 数値はすべて Inspector 調整可(ノブ雛形)。ミニゲーム種類は MicroGameRegistry に登録するだけで増える。
/// </summary>
public class ActionCommandPhase : MonoBehaviour, IBattlePhase
{
    [Header("依存(未設定なら自動探索)")]
    [SerializeField] private BattleStateMachine stateMachine;
    [SerializeField] private CardSelectPhase cardSelectPhase;
    [SerializeField] private PartSelectPhase partSelectPhase;
    [SerializeField] private EnemyRuntimeState enemyState;
    [SerializeField] private BattleEffectProcessor effectProcessor;
    [Tooltip("数値の唯一の真実(Excel/CSV連動)。設定で下記既定値を上書き。")]
    [SerializeField] private GameTuning tuning;

    [Header("進行")]
    [Tooltip("プレイヤー人数。")]
    [Min(1)] public int playerCount = 4;
    [Tooltip("1フェーズで行うミニゲーム(ラウンド)数。")]
    [Min(1)] public int roundsPerActionCommand = 3;
    [Tooltip("ラウンド間の小休止(秒)。")]
    public float interRoundDelay = 0.7f;
    [Tooltip("成否表示の秒数。")]
    public float resultFlashDuration = 0.9f;

    [Header("難易度ノブ雛形(Inspector調整)")]
    public float baseTimeLimit = 3.5f;        // 4.5f → 3.5f (短縮)
    public float timeLimitPerRound = -0.5f;   // -0.7f → -0.5f (緩和、早く終わりすぎないように)
    public float speedRampPerRound = 0.25f;   // 0.18f → 0.25f (速度増加)
    public int buttonCountBase = 3;           // 2 → 3 (ボタン数増加)
    [Tooltip("選択カード難度合計1点あたりの speed 加算。")]
    public float difficultyToSpeed = 0.05f;   // 0.03f → 0.05f (難度影響増加)
    [Tooltip("同 interference 加算。")]
    public float difficultyToInterfere = 0.02f; // 0.012f → 0.02f (妨害増加)
    public float minTimeLimit = 1.2f;         // 1.6f → 1.2f (最小制限時間短縮)

    [Header("表示")]
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.72f, 0.72f, 1f),   // 薄い赤
        new Color(0.72f, 0.83f, 1f, 1f),   // 薄い青
        new Color(0.72f, 1f, 0.72f, 1f),   // 薄い緑
        new Color(1f, 0.93f, 0.72f, 1f),   // 薄い黄
    };

    /// <summary>このフェーズでの成功数(次フェーズ等が参照可能)。</summary>
    public int SuccessCount { get; private set; }
    /// <summary>実行したラウンド総数。</summary>
    public int RoundsPlayed { get; private set; }
    /// <summary>各プレイヤーの成功回数(部位破壊のダメージ適用判定に使う)。</summary>
    public IReadOnlyList<int> PerPlayerSuccess => perPlayerSuccess;

    public bool BlocksDebugAdvance => true; // 自前で入力処理&遷移するためデバッグEnter抑止

    private Coroutine routine;
    private MiniGameStartView startView;
    private GameObject uiHost;
    private RectTransform microRoot;
    private System.Random rng;
    private int[] perPlayerSuccess;

    // IMGUI用
    private ActionCommandIMGUIHelper guiHelper;
    private string bannerText = "";
    private List<HitEffectData> hitEffects = new List<HitEffectData>();

    public class HitEffectData
    {
        public string text;
        public Color color;
        public float x;
        public float startTime;
        public float duration;
    }

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (cardSelectPhase == null) cardSelectPhase = FindObjectOfType<CardSelectPhase>();
        if (partSelectPhase == null) partSelectPhase = FindObjectOfType<PartSelectPhase>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (effectProcessor == null) effectProcessor = FindObjectOfType<BattleEffectProcessor>();
        ApplyTuning();
    }

    private void ApplyTuning()
    {
        if (tuning == null) return;
        playerCount = tuning.playerCount;
        roundsPerActionCommand = tuning.roundsPerActionCommand;
        interRoundDelay = tuning.interRoundDelay;
        resultFlashDuration = tuning.resultFlashDuration;
        baseTimeLimit = tuning.baseTimeLimit;
        timeLimitPerRound = tuning.timeLimitPerRound;
        speedRampPerRound = tuning.speedRampPerRound;
        buttonCountBase = tuning.buttonCountBase;
        difficultyToSpeed = tuning.difficultyToSpeed;
        difficultyToInterfere = tuning.difficultyToInterfere;
        minTimeLimit = tuning.minTimeLimit;
        var cols = tuning.ResolvePlayerColors();
        if (cols != null && cols.Length > 0) playerColors = cols;
    }

    void Start() { enabled = false; }

    public void Enter()
    {
        enabled = true;

        // HPバーを非表示
        var hpGauge = FindObjectOfType<SharedHpGaugeUI>();
        if (hpGauge != null) hpGauge.SetVisible(false);

        SuccessCount = 0;
        RoundsPlayed = 0;
        perPlayerSuccess = new int[Mathf.Max(1, playerCount)];
        rng = new System.Random();
        hitEffects.Clear();
        bannerText = "";

        // IMGUIヘルパーを追加
        guiHelper = gameObject.AddComponent<ActionCommandIMGUIHelper>();
        guiHelper.Initialize(this);

        UnityEngine.Debug.Log("[ActionCommandPhase] 開始");
        routine = StartCoroutine(RunSequence());
    }

    public void Exit()
    {
        enabled = false;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        StopAllCoroutines();
        if (startView != null) { startView.Close(); Destroy(startView.gameObject); startView = null; }
        if (uiHost != null) { Destroy(uiHost); uiHost = null; microRoot = null; }
        if (guiHelper != null) { Destroy(guiHelper); guiHelper = null; }
        hitEffects.Clear();
        bannerText = "";
        UnityEngine.Debug.Log("[ActionCommandPhase] 終了");
    }

    public void Tick() { /* 進行はコルーチン駆動 */ }

    public string GetBannerText() => bannerText;
    public List<HitEffectData> GetHitEffects() => hitEffects;
    public int GetPlayerCount() => playerCount;
    public Color[] GetPlayerColors() => playerColors;

    private IEnumerator RunSequence()
    {
        var selections = cardSelectPhase != null ? cardSelectPhase.Selections : null;
        var mods = new ActionCommandModifiers(selections);

        // Registry からランダムにミニゲームを選出
        var registry = MicroGameRegistry.CreateDefault();
        string chosenName;
        IMicroGame game = registry.CreateRandom(rng, out chosenName);
        
        // 万が一 Registry が空の場合は GroupButton にフォールバック
        if (game == null)
        {
            game = new GroupButtonMicroGame();
            chosenName = game.DisplayName;
        }

        // MiniGameStart: 全員が順に決定
        var svObj = new GameObject("MiniGameStartView");
        svObj.transform.SetParent(transform, false);
        startView = svObj.AddComponent<MiniGameStartView>();
        startView.Show(chosenName, playerCount, playerColors);
        while (!startView.Tick()) yield return null;
        yield return new WaitForSeconds(0.25f);
        startView.Close();
        Destroy(startView.gameObject);
        startView = null;

        // ミニゲーム用 UI ルート
        var canvas = AcUi.CreateOverlayCanvas(transform, "ActionCommandCanvas", 110);
        uiHost = canvas.gameObject;
        var rootGo = new GameObject("MicroRoot", typeof(RectTransform));
        rootGo.transform.SetParent(canvas.transform, false);
        microRoot = rootGo.GetComponent<RectTransform>();
        AcUi.Stretch(microRoot);

        // 難易度・修飾値の構築
        var difficultyTuning = new ActionCommandModifiers.Tuning
        {
            baseTimeLimit = baseTimeLimit,
            timeLimitPerRound = timeLimitPerRound,
            speedRampPerRound = speedRampPerRound,
            buttonCountBase = buttonCountBase,
            difficultyToSpeed = difficultyToSpeed,
            difficultyToInterfere = difficultyToInterfere,
            minTimeLimit = minTimeLimit,
        };

        bannerText = chosenName;

        // グループ参加型(GroupButton)か、個別順次参加型かを判定
        // 現状 GroupButtonMicroGame 以外は 1人用として実装されているため、
        // 1人用の場合は人数分ループさせる(本来のパーティゲームの挙動)。
        bool isGroupGame = (game is GroupButtonMicroGame);

        if (isGroupGame)
        {
            var k = mods.Resolve(0, difficultyTuning);
            var ctx = new MicroGameContext
            {
                uiRoot = microRoot,
                rng = rng,
                roundIndex = 0,
                playerIndex = 0,
                playerCount = playerCount,
                timeLimit = k.timeLimit,
                speedMul = k.speedMul,
                buttonCount = k.buttonCount,
                rangeScale = k.rangeScale,
                interference = k.interference,
                accent = (playerColors != null && playerColors.Length > 0) ? playerColors[0] : Color.white,
                playerColors = playerColors,
                OnPlayerAttack = HandlePlayerAttack,
            };

            game.Begin(ctx);
            bool done = false;
            while (!done)
            {
                done = game.Tick(Time.deltaTime);
                yield return null;
            }
            game.Cleanup();
            RoundsPlayed = 1;
        }
        else
        {
            // 順次参加型: 1人ずつミニゲームを実行
            for (int p = 0; p < playerCount; p++)
            {
                // 各プレイヤーのターン開始前に少し待機
                if (p > 0) yield return new WaitForSeconds(interRoundDelay);

                var k = mods.Resolve(p, difficultyTuning);
                var ctx = new MicroGameContext
                {
                    uiRoot = microRoot,
                    rng = rng,
                    roundIndex = p,
                    playerIndex = p,
                    playerCount = 1, // このプレイヤーのみ
                    timeLimit = k.timeLimit,
                    speedMul = k.speedMul,
                    buttonCount = k.buttonCount,
                    rangeScale = k.rangeScale,
                    interference = k.interference,
                    accent = (playerColors != null && p < playerColors.Length) ? playerColors[p] : Color.white,
                    playerColors = playerColors,
                    OnPlayerAttack = HandlePlayerAttack,
                };

                // 2回目以降は同じ種類のゲームインスタンスを再生成(Cleanup/Beginを呼ぶため)
                if (p > 0) game = registry.CreateRandom(rng, out _); // 同じ種類固定にしたい場合は調整

                game.Begin(ctx);
                bool done = false;
                while (!done)
                {
                    done = game.Tick(Time.deltaTime);
                    yield return null;
                }

                if (game.Succeeded)
                {
                    // 個別ゲームの場合は成功時に自分で HandlePlayerAttack を呼ぶ必要がある
                    // (Mash 等が OnPlayerAttack を呼んでいないため)
                    HandlePlayerAttack(p);
                }

                game.Cleanup();
                RoundsPlayed++;
                
                // 次のプレイヤーへのバナー更新
                if (p < playerCount - 1)
                {
                    bannerText = $"NEXT: P{p + 2}";
                    yield return new WaitForSeconds(interRoundDelay);
                    bannerText = chosenName;
                }
            }
        }

        // 集計表示(命中数=SuccessCount)
        bannerText = $"HIT {SuccessCount}/{playerCount}";
        yield return new WaitForSeconds(resultFlashDuration);

        UnityEngine.Debug.Log($"[ActionCommandPhase] 命中 {SuccessCount}/{playerCount} → BattleResult へ");
        routine = null;
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.BattleResult);
    }

    /// <summary>
    /// GroupButtonMicroGame から、プレイヤー N が命中した瞬間に呼ばれる。
    /// 1) 「+ダメージ」演出
    /// 2) 攻撃カードなら EnemyRuntimeState に即時ダメージ反映
    /// 3) 非攻撃カードは BattleEffectProcessor で処理
    /// 集計のための perPlayerSuccess も加算する。
    /// </summary>
    private void HandlePlayerAttack(int playerIndex)
    {
        var selections = cardSelectPhase != null ? cardSelectPhase.Selections : null;
        if (selections == null || playerIndex < 0 || playerIndex >= selections.Count) return;
        var card = selections[playerIndex];
        if (card == null || card.Data == null) return;

        if (perPlayerSuccess != null && playerIndex < perPlayerSuccess.Length)
            perPlayerSuccess[playerIndex]++;
        SuccessCount++;

        int dmg = 0;
        string hitLabel = $"P{playerIndex + 1}";

        // 攻撃カードの場合: 敵にダメージ
        if (card.Category == CardCategory.Attack && enemyState != null)
        {
            // バフによる攻撃力補正を適用
            int basePower = card.EffectivePower;
            dmg = effectProcessor != null ? effectProcessor.CalculateAttackPower(basePower) : basePower;
            dmg = Mathf.Max(0, dmg);

            if (card.Data.attackRange == CardAttackRange.All)
            {
                var alive = new List<string>(enemyState.EnumerateAliveParts());
                foreach (var pid in alive) enemyState.ApplyDamage(pid, dmg);
                hitLabel += $" -{dmg}";
            }
            else
            {
                var targets = partSelectPhase != null ? partSelectPhase.TargetPartIds : null;
                string partId = (targets != null && playerIndex < targets.Count) ? targets[playerIndex] : null;
                if (!string.IsNullOrEmpty(partId))
                {
                    enemyState.ApplyDamage(partId, dmg);
                    hitLabel += $" -{dmg}";
                }
            }
        }
        // 非攻撃カード(回復/防御/バフ/特殊)の場合: BattleEffectProcessor で処理
        else if (effectProcessor != null)
        {
            int successCount = (perPlayerSuccess != null && playerIndex < perPlayerSuccess.Length) ? perPlayerSuccess[playerIndex] : 1;
            effectProcessor.ProcessCardEffect(playerIndex, card, successCount);

            if (card.Category == CardCategory.Heal)
                hitLabel += $" +{card.EffectivePower}";
            else if (card.Category == CardCategory.Defend)
                hitLabel += " DEF";
            else if (card.Category == CardCategory.Buff)
                hitLabel += " BUFF";
            else
                hitLabel += " OK";
        }

        // ヒットエフェクトを追加
        float slotW = 320f;
        float gap = 30f;
        float total = playerCount * slotW + (playerCount - 1) * gap;
        float startX = -total * 0.5f + slotW * 0.5f;
        float x = startX + playerIndex * (slotW + gap);

        Color col = (playerColors != null && playerIndex < playerColors.Length)
            ? playerColors[playerIndex] : Color.white;

        hitEffects.Add(new HitEffectData
        {
            text = hitLabel,
            color = col,
            x = x,
            startTime = Time.time,
            duration = 1.0f
        });
    }

    private static string CardCategoryJa(CardCategory cat)
    {
        switch (cat)
        {
            case CardCategory.Attack: return "攻撃";
            case CardCategory.Defend: return "防御";
            case CardCategory.Heal: return "回復";
            case CardCategory.Buff: return "バフ";
            default: return "特殊";
        }
    }
}

/// <summary>ActionCommandPhase用のIMGUIヘルパー</summary>
public class ActionCommandIMGUIHelper : MonoBehaviour
{
    private ActionCommandPhase phase;
    private int pixelScale;

    private Texture2D bannerTexture;
    private string cachedBanner;
    private Dictionary<string, Texture2D> hitTextures = new Dictionary<string, Texture2D>();

    public void Initialize(ActionCommandPhase p)
    {
        phase = p;
        pixelScale = Mathf.Max(3, Screen.height / 240);
    }

    void OnDestroy()
    {
        if (bannerTexture != null) Destroy(bannerTexture);
        foreach (var tex in hitTextures.Values)
            if (tex != null) Destroy(tex);
        hitTextures.Clear();
    }

    void OnGUI()
    {
        if (phase == null) return;

        // バナー描画
        string banner = phase.GetBannerText();
        if (!string.IsNullOrEmpty(banner))
        {
            if (banner != cachedBanner || bannerTexture == null)
            {
                if (bannerTexture != null) Destroy(bannerTexture);
                bannerTexture = PixelFontRenderer.RenderText(banner, Color.white, pixelScale + 1);
                cachedBanner = banner;
            }

            if (bannerTexture != null)
            {
                float x = (Screen.width - bannerTexture.width) / 2f;
                float y = Screen.height * 0.08f;
                GUI.DrawTexture(new Rect(x, y, bannerTexture.width, bannerTexture.height), bannerTexture);
            }
        }

        // ヒットエフェクト描画
        var effects = phase.GetHitEffects();
        float currentTime = Time.time;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            var effect = effects[i];
            float elapsed = currentTime - effect.startTime;

            if (elapsed >= effect.duration)
            {
                effects.RemoveAt(i);
                continue;
            }

            float progress = elapsed / effect.duration;
            float alpha = 1f - progress * progress;

            // テクスチャ取得/作成
            string key = $"{effect.text}_{effect.color.r}_{effect.color.g}_{effect.color.b}";
            if (!hitTextures.TryGetValue(key, out Texture2D tex) || tex == null)
            {
                tex = PixelFontRenderer.RenderText(effect.text, effect.color, pixelScale + 2);
                hitTextures[key] = tex;
            }

            if (tex != null)
            {
                float screenX = Screen.width / 2f + effect.x - tex.width / 2f;
                float baseY = Screen.height * 0.4f;
                float yOffset = 90f * progress;

                Color c = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(screenX, baseY - yOffset, tex.width, tex.height), tex);
                GUI.color = c;
            }
        }
    }
}
