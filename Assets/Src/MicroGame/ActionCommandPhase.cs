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

    [Header("【仮】ボス撃破条件")]
    [Tooltip("アクションコマンド成功が累計これだけ溜まるとボス撃破(勝利)。四人分成功で倒せる想定=4")]
    [SerializeField] private int hitsToDefeatBoss = 4;
    [Tooltip("非攻撃カード(回復/防御/バフ等)がデフォルト攻撃で与える基礎威力")]
    [SerializeField] private int nonAttackDefaultPower = 1;
    // 累計成功数。ラウンドをまたいで蓄積する(Enterでリセットしない)。
    private int cumulativeHits;

    // ダメージ演出用: 敵(ボス)の視覚参照とホーム位置・揺れコルーチン
    private Transform cachedEnemyTf;
    private Vector3 cachedEnemyHome;
    private Coroutine enemyShakeCo;
    // 実行中ミニゲームのコンテキスト(ダメージ光の始点=ゲージ末端座標の取得に使う)
    private MicroGameContext currentCtx;
    /// <summary>実行したラウンド総数。</summary>
    public int RoundsPlayed { get; private set; }
    /// <summary>各プレイヤーの成功回数(部位破壊のダメージ適用判定に使う)。</summary>
    public IReadOnlyList<int> PerPlayerSuccess => perPlayerSuccess;

    public bool BlocksDebugAdvance => true; // 自前で入力処理&遷移するためデバッグEnter抑止

    private Coroutine routine;
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

        // 【仮】アクションコマンドは「4人分の領域に分かれた大ゲージをスペース連打」(グループ型)に固定。
        string chosenName;
        IMicroGame game = new GroupMashMicroGame();
        chosenName = game.DisplayName;

        // MiniGameStart 画面は出さず、そのままミニゲームを開始する。
        // (CardSelect のカーソル準備完了と二重になり、一瞬表示が出てしまうため廃止)
        UnityEngine.Debug.Log($"[ActionCommandPhase] ミニゲーム開始: {chosenName}");

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
        bool isGroupGame = (game is GroupButtonMicroGame) || (game is GroupMashMicroGame);

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

            currentCtx = ctx;
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

                // 2回目以降も同じスペース連打(Mash)を再生成(Cleanup/Begin のライフサイクルのため)
                if (p > 0) game = new MashMicroGame();

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

        routine = null;

        // 【仮】累計成功が閾値に達したらボス撃破＝勝利。リザルト(勝利)へ。
        if (cumulativeHits >= hitsToDefeatBoss)
        {
            UnityEngine.Debug.Log($"[ActionCommandPhase] 累計命中 {cumulativeHits}/{hitsToDefeatBoss} → ボス撃破！勝利");
            var bsc = FindObjectOfType<BattleSceneController>();
            if (bsc != null) bsc.OnBattleVictory();
            else GameSceneManager.Instance?.GoToResult(true);
            yield break;
        }

        UnityEngine.Debug.Log($"[ActionCommandPhase] 命中 {SuccessCount}/{playerCount} → BattleResult へ");
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
        cumulativeHits++;

        int dmg = 0;
        string hitLabel = $"P{playerIndex + 1}";

        // 【仕様】どのカードでも「デフォルト攻撃」を行う。選択した部位(なければ生存部位)にダメージ。
        // 攻撃カードはカード威力、非攻撃カードは nonAttackDefaultPower を基礎威力にする。
        if (enemyState != null)
        {
            int basePower = (card.Category == CardCategory.Attack) ? card.EffectivePower : nonAttackDefaultPower;
            dmg = effectProcessor != null ? effectProcessor.CalculateAttackPower(basePower) : basePower;
            dmg = Mathf.Max(1, dmg);

            var targets = partSelectPhase != null ? partSelectPhase.TargetPartIds : null;
            string partId = (targets != null && playerIndex < targets.Count) ? targets[playerIndex] : null;
            if (string.IsNullOrEmpty(partId))
            {
                // 部位未指定なら生存部位のいずれかへ
                var alive = new List<string>(enemyState.EnumerateAliveParts());
                partId = alive.Count > 0 ? alive[0] : null;
            }
            if (!string.IsNullOrEmpty(partId))
            {
                enemyState.ApplyDamage(partId, dmg);
                hitLabel += $" -{dmg}";
            }
        }

        // 非攻撃カードは、デフォルト攻撃に加えて本来の効果も処理する。
        if (card.Category != CardCategory.Attack && effectProcessor != null)
        {
            int successCount = (perPlayerSuccess != null && playerIndex < perPlayerSuccess.Length) ? perPlayerSuccess[playerIndex] : 1;
            effectProcessor.ProcessCardEffect(playerIndex, card, successCount);

            if (card.Category == CardCategory.Heal)
                hitLabel += $" +{card.EffectivePower}";
            else if (card.Category == CardCategory.Defend)
                hitLabel += " DEF";
            else if (card.Category == CardCategory.Buff)
                hitLabel += " BUFF";
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

        // 成功時のみ: そのプレイヤーカラーの光が敵に飛び込み、敵が揺れる。
        StartCoroutine(PlayDamageLightAndShake(playerIndex, col));
    }

    /// <summary>敵(ボス)の視覚 Transform を取得(見つからなければ null)。ホーム位置もキャッシュ。</summary>
    private Transform FindEnemyTransform()
    {
        if (cachedEnemyTf != null) return cachedEnemyTf;
        var ctrl = FindObjectOfType<EnemyOnFieldController>();
        if (ctrl != null) { cachedEnemyTf = ctrl.transform; cachedEnemyHome = cachedEnemyTf.position; return cachedEnemyTf; }
        var byName = GameObject.Find("Boss");
        if (byName != null) { cachedEnemyTf = byName.transform; cachedEnemyHome = cachedEnemyTf.position; return cachedEnemyTf; }
        return null;
    }

    /// <summary>プレイヤー色の光を敵へ飛ばし(シュッ)、到達後に敵を揺らす。</summary>
    private IEnumerator PlayDamageLightAndShake(int playerIndex, Color col)
    {
        var enemy = FindEnemyTransform();
        if (enemy == null) yield break;

        Vector3 target = cachedEnemyHome;

        // 始点: ゲージ末端(スクリーン座標)を敵の平面へ逆投影。取れなければ敵の下から。
        Vector3 start;
        var cam = Camera.main;
        if (cam != null && currentCtx != null && currentCtx.HasLastAttackScreenPos)
        {
            var sp = currentCtx.LastAttackScreenPos;
            float dist = Mathf.Abs(cam.transform.position.z - target.z);
            start = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, dist));
            start.z = target.z;
        }
        else
        {
            float offsetX = (playerIndex - (playerCount - 1) * 0.5f) * 0.7f;
            start = target + new Vector3(offsetX, -4.5f, 0f);
        }

        // 光(大きめ・分かりやすく)
        var tex = PixelFontRenderer.CreatePixelStar(30, col);
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        var lightGo = new GameObject("DamageLight");
        var sr = lightGo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 300;
        lightGo.transform.position = start;

        float dur = 0.18f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float ease = 1f - (1f - p) * (1f - p);
            lightGo.transform.position = Vector3.Lerp(start, target, ease);
            lightGo.transform.localScale = Vector3.one * Mathf.Lerp(2.6f, 1.0f, p);
            yield return null;
        }
        Destroy(lightGo);

        // 揺れ(前の揺れが残っていれば復帰させてから開始し、位置ドリフトを防ぐ)
        if (enemyShakeCo != null) { StopCoroutine(enemyShakeCo); if (cachedEnemyTf != null) cachedEnemyTf.position = cachedEnemyHome; }
        enemyShakeCo = StartCoroutine(ShakeEnemy(col));
    }

    /// <summary>敵を短時間ぶるっと揺らし、スプライトを一瞬フラッシュさせる。</summary>
    private IEnumerator ShakeEnemy(Color flash)
    {
        var enemy = cachedEnemyTf;
        if (enemy == null) yield break;

        var sr = enemy.GetComponentInChildren<SpriteRenderer>();
        Color baseCol = sr != null ? sr.color : Color.white;
        if (sr != null) sr.color = Color.Lerp(baseCol, Color.white, 0.65f);

        float dur = 0.26f, t = 0f, amp = 0.2f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float damp = 1f - t / dur;
            float ox = (UnityEngine.Random.value * 2f - 1f) * amp * damp;
            float oy = (UnityEngine.Random.value * 2f - 1f) * amp * damp;
            if (enemy != null) enemy.position = cachedEnemyHome + new Vector3(ox, oy, 0f);
            yield return null;
        }
        if (enemy != null) enemy.position = cachedEnemyHome;
        if (sr != null) sr.color = baseCol;
        enemyShakeCo = null;
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
