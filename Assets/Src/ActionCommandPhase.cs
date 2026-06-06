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
    public float baseTimeLimit = 4.5f;
    public float timeLimitPerRound = -0.7f;
    public float speedRampPerRound = 0.18f;
    public int buttonCountBase = 2;
    [Tooltip("選択カード難度合計1点あたりの speed 加算。")]
    public float difficultyToSpeed = 0.03f;
    [Tooltip("同 interference 加算。")]
    public float difficultyToInterfere = 0.012f;
    public float minTimeLimit = 1.6f;

    [Header("表示")]
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.35f, 0.35f, 1f),
        new Color(0.35f, 0.6f, 1f, 1f),
        new Color(0.35f, 1f, 0.4f, 1f),
        new Color(1f, 0.85f, 0.35f, 1f),
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

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (cardSelectPhase == null) cardSelectPhase = FindObjectOfType<CardSelectPhase>();
        if (partSelectPhase == null) partSelectPhase = FindObjectOfType<PartSelectPhase>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
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
        SuccessCount = 0;
        RoundsPlayed = 0;
        perPlayerSuccess = new int[Mathf.Max(1, playerCount)];
        rng = new System.Random();
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
        UnityEngine.Debug.Log("[ActionCommandPhase] 終了");
    }

    public void Tick() { /* 進行はコルーチン駆動 */ }

    private IEnumerator RunSequence()
    {
        var selections = cardSelectPhase != null ? cardSelectPhase.Selections : null;
        var mods = new ActionCommandModifiers(selections);

        // グループ式: 全員で1つのミニゲームを遊ぶ。命中の瞬間に OnPlayerAttack コールバックが
        // 個別に発火し、即座にダメージ+演出が走る(集計はもう要らない)。
        IMicroGame game = new GroupButtonMicroGame();
        string chosenName = game.DisplayName;

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

        var banner = AcUi.Label(canvas.transform, "Banner", 40, Color.white,
            TextAnchor.UpperCenter, true, true);
        AcUi.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1400f, 70f));
        banner.text = $"ミニゲーム: {chosenName}  全員一斉に勝負!";

        game.Begin(ctx);
        bool done = false;
        while (!done)
        {
            done = game.Tick(Time.deltaTime);
            yield return null;
        }
        game.Cleanup();
        RoundsPlayed = 1;

        // 集計表示(命中数=SuccessCount)
        banner.text = $"アクションコマンド終了  命中 {SuccessCount} / {playerCount}";
        yield return new WaitForSeconds(resultFlashDuration);

        UnityEngine.Debug.Log($"[ActionCommandPhase] 命中 {SuccessCount}/{playerCount} → Dodge へ");
        routine = null;
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.Dodge);
    }

    /// <summary>
    /// GroupButtonMicroGame から、プレイヤー N が命中した瞬間に呼ばれる。
    /// 1) 「+ダメージ」演出
    /// 2) 攻撃カードなら EnemyRuntimeState に即時ダメージ反映
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
        string hitLabel = $"P{playerIndex + 1}  {card.Data.displayName}";
        if (card.Category == CardCategory.Attack && enemyState != null)
        {
            dmg = Mathf.Max(0, card.EffectivePower);
            if (card.Data.attackRange == CardAttackRange.All)
            {
                var alive = new List<string>(enemyState.EnumerateAliveParts());
                foreach (var pid in alive) enemyState.ApplyDamage(pid, dmg);
                hitLabel += $"\n全体 -{dmg} × {alive.Count}";
            }
            else
            {
                var targets = partSelectPhase != null ? partSelectPhase.TargetPartIds : null;
                string partId = (targets != null && playerIndex < targets.Count) ? targets[playerIndex] : null;
                if (!string.IsNullOrEmpty(partId))
                {
                    enemyState.ApplyDamage(partId, dmg);
                    var part = enemyState.enemyData?.FindPart(partId);
                    hitLabel += $"\n{(part != null ? part.displayName : partId)} -{dmg}";
                }
                else
                {
                    hitLabel += "\n(部位未指定)";
                }
            }
        }
        else if (card.Category != CardCategory.Attack)
        {
            hitLabel += $"\n[{CardCategoryJa(card.Category)}発動]";
        }

        StartCoroutine(SpawnHitEffect(playerIndex, hitLabel, dmg));
    }

    /// <summary>命中効果テキスト: 文字だけがふわっと上に浮かんで消える(枠/背景なし)。</summary>
    private IEnumerator SpawnHitEffect(int playerIndex, string label, int dmg)
    {
        if (uiHost == null) yield break;
        var canvas = uiHost.GetComponentInChildren<Canvas>() ?? uiHost.GetComponent<Canvas>();
        if (canvas == null) yield break;

        Color col = (playerColors != null && playerIndex < playerColors.Length)
            ? playerColors[playerIndex] : Color.white;

        // GroupButtonMicroGame のスロット位置に合わせて X を決める(同じ配置式)
        float slotW = 320f;
        float gap = 30f;
        float total = playerCount * slotW + (playerCount - 1) * gap;
        float startX = -total * 0.5f + slotW * 0.5f;
        float x = startX + playerIndex * (slotW + gap);

        var bigLabel = AcUi.Label(canvas.transform, $"HitFx_{playerIndex}", 64, col,
            TextAnchor.MiddleCenter, true, true);
        AcUi.Place(bigLabel.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(x, 140f), new Vector2(slotW + 120f, 200f));
        bigLabel.text = label;

        float t = 0f, dur = 1.0f;
        Vector2 startPos = bigLabel.rectTransform.anchoredPosition;
        Color startCol = bigLabel.color;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            bigLabel.rectTransform.anchoredPosition = startPos + new Vector2(0f, 90f * p);
            var c2 = startCol;
            c2.a = 1f - p * p;
            bigLabel.color = c2;
            yield return null;
        }
        if (bigLabel != null) Destroy(bigLabel.gameObject);
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
