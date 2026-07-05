using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アクションコマンドフェーズ後、ダメージ結果を演出表示するフェーズ。
/// 各プレイヤーの攻撃結果、部位破壊、ボス撃破判定などを表示し、
/// 次のフェーズ(Dodge or 勝利)へ遷移する。
/// </summary>
public class BattleResultPhase : MonoBehaviour, IBattlePhase
{
    [Header("依存(未設定なら自動探索)")]
    [SerializeField] private BattleStateMachine stateMachine;
    [SerializeField] private ActionCommandPhase actionCommandPhase;
    [SerializeField] private CardSelectPhase cardSelectPhase;
    [SerializeField] private EnemyRuntimeState enemyState;
    [SerializeField] private SharedHpController sharedHpController;
    [SerializeField] private GameTuning tuning;

    [Header("表示設定")]
    [Tooltip("結果表示の各項目の表示間隔(秒)")]
    public float itemDisplayInterval = 0.3f;
    [Tooltip("結果表示後の待機時間(秒)")]
    public float postResultDelay = 1.0f;
    [Tooltip("部位破壊演出の時間(秒)")]
    public float partDestroyEffectDuration = 0.8f;

    public bool BlocksDebugAdvance => true;

    private Coroutine routine;
    private BattleResultIMGUIHelper guiHelper;

    // 表示用データ
    private List<ResultEntry> resultEntries = new List<ResultEntry>();
    private List<string> destroyedPartsThisTurn = new List<string>();
    private bool bossDefeated = false;
    private string currentMessage = "";
    private float messageAlpha = 1f;

    public class ResultEntry
    {
        public int playerIndex;
        public string cardName;
        public CardCategory category;
        public int damage;
        public int healAmount;
        public string targetPartName;
        public bool success;
        public Color playerColor;
        public float displayTime;
    }

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (actionCommandPhase == null) actionCommandPhase = FindObjectOfType<ActionCommandPhase>();
        if (cardSelectPhase == null) cardSelectPhase = FindObjectOfType<CardSelectPhase>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (sharedHpController == null) sharedHpController = FindObjectOfType<SharedHpController>();
    }

    void Start() { enabled = false; }

    public void Enter()
    {
        enabled = true;
        resultEntries.Clear();
        destroyedPartsThisTurn.Clear();
        bossDefeated = false;
        currentMessage = "";
        messageAlpha = 1f;

        // HPバーを非表示
        var hpGauge = FindObjectOfType<SharedHpGaugeUI>();
        if (hpGauge != null) hpGauge.SetVisible(false);

        // IMGUIヘルパーを追加
        guiHelper = gameObject.AddComponent<BattleResultIMGUIHelper>();
        guiHelper.Initialize(this);

        // 部位破壊イベントを購読
        if (enemyState != null)
        {
            enemyState.OnPartDestroyed += OnPartDestroyed;
        }

        Debug.Log("[BattleResultPhase] 開始");
        routine = StartCoroutine(RunResultSequence());
    }

    public void Exit()
    {
        enabled = false;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        StopAllCoroutines();

        if (enemyState != null)
        {
            enemyState.OnPartDestroyed -= OnPartDestroyed;
        }

        if (guiHelper != null) { Destroy(guiHelper); guiHelper = null; }
        resultEntries.Clear();
        destroyedPartsThisTurn.Clear();
        currentMessage = "";
        Debug.Log("[BattleResultPhase] 終了");
    }

    public void Tick() { /* コルーチン駆動 */ }

    private void OnPartDestroyed(EnemyPart part)
    {
        if (part != null && !string.IsNullOrEmpty(part.displayName))
        {
            destroyedPartsThisTurn.Add(part.displayName);
        }
    }

    private IEnumerator RunResultSequence()
    {
        var selections = cardSelectPhase?.Selections;
        var perPlayerSuccess = actionCommandPhase?.PerPlayerSuccess;
        int playerCount = actionCommandPhase?.GetPlayerCount() ?? 4;
        var playerColors = actionCommandPhase?.GetPlayerColors();

        // 1. 各プレイヤーの結果をエントリーに追加
        currentMessage = "RESULT";
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < playerCount; i++)
        {
            var card = (selections != null && i < selections.Count) ? selections[i] : null;
            int successCount = (perPlayerSuccess != null && i < perPlayerSuccess.Count) ? perPlayerSuccess[i] : 0;

            var entry = new ResultEntry
            {
                playerIndex = i,
                cardName = card?.Data?.displayName ?? "---",
                category = card?.Category ?? CardCategory.Attack,
                damage = (card?.Category == CardCategory.Attack && successCount > 0) ? card.EffectivePower * successCount : 0,
                healAmount = (card?.Category == CardCategory.Heal && successCount > 0) ? card.EffectivePower * successCount : 0,
                success = successCount > 0,
                playerColor = (playerColors != null && i < playerColors.Length) ? playerColors[i] : Color.white,
                displayTime = Time.time
            };

            resultEntries.Add(entry);
            yield return new WaitForSeconds(itemDisplayInterval);
        }

        // 2. 部位破壊があれば演出
        if (destroyedPartsThisTurn.Count > 0)
        {
            yield return new WaitForSeconds(0.3f);
            foreach (var partName in destroyedPartsThisTurn)
            {
                currentMessage = $"BREAK! {partName}";
                yield return new WaitForSeconds(partDestroyEffectDuration);
            }
        }

        // 3. ボス撃破判定
        if (enemyState != null && IsAllPartsDestroyed())
        {
            bossDefeated = true;
            currentMessage = "BOSS DEFEATED!";
            yield return new WaitForSeconds(1.5f);

            // 勝利処理へ
            routine = null;
            HandleBossDefeated();
            yield break;
        }

        // 4. 通常遷移(Dodgeフェーズへ)
        currentMessage = "DODGE START!";
        yield return new WaitForSeconds(postResultDelay);

        routine = null;
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.Dodge);
    }

    private bool IsAllPartsDestroyed()
    {
        if (enemyState == null || enemyState.enemyData == null) return false;
        var parts = enemyState.enemyData.parts;
        if (parts == null || parts.Count == 0) return false;

        foreach (var part in parts)
        {
            if (part == null || string.IsNullOrEmpty(part.id)) continue;
            if (!enemyState.IsDestroyed(part.id)) return false;
        }
        return true;
    }

    private void HandleBossDefeated()
    {
        var run = FindObjectOfType<RunProgress>();
        if (run != null)
        {
            run.OnBossDefeated();
            bool stageDone = run.currentStage == null || run.currentBossIndex >= run.currentStage.BossCount;
            if (stageDone)
            {
                if (GameSceneManager.Instance != null)
                    GameSceneManager.Instance.GoToResult(true);
                return;
            }
            else
            {
                // 次のボスへ
                if (enemyState != null && run.CurrentBoss != null)
                {
                    enemyState.enemyData = run.CurrentBoss;
                    enemyState.ResetAll();
                }
            }
        }

        // カード選択フェーズへ戻る
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.CardSelect);
    }

    // GUI用アクセサ
    public string GetCurrentMessage() => currentMessage;
    public float GetMessageAlpha() => messageAlpha;
    public List<ResultEntry> GetResultEntries() => resultEntries;
    public bool IsBossDefeated() => bossDefeated;
    public List<string> GetDestroyedParts() => destroyedPartsThisTurn;
}

/// <summary>BattleResultPhase用のIMGUIヘルパー</summary>
public class BattleResultIMGUIHelper : MonoBehaviour
{
    private BattleResultPhase phase;
    private int pixelScale;
    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

    public void Initialize(BattleResultPhase p)
    {
        phase = p;
        pixelScale = Mathf.Max(3, Screen.height / 240);
    }

    void OnDestroy()
    {
        foreach (var tex in textureCache.Values)
            if (tex != null) Destroy(tex);
        textureCache.Clear();
    }

    void OnGUI()
    {
        if (phase == null) return;

        // メインメッセージ
        string msg = phase.GetCurrentMessage();
        if (!string.IsNullOrEmpty(msg))
        {
            DrawCenteredText(msg, Screen.height * 0.15f, pixelScale + 2, Color.white);
        }

        // 結果エントリー表示
        var entries = phase.GetResultEntries();
        float startY = Screen.height * 0.3f;
        float entryHeight = 50f;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            float y = startY + i * entryHeight;
            DrawResultEntry(entry, y);
        }

        // ボス撃破時の特別表示
        if (phase.IsBossDefeated())
        {
            DrawCenteredText("VICTORY!", Screen.height * 0.5f, pixelScale + 4, new Color(1f, 0.85f, 0.2f));
        }
    }

    private void DrawResultEntry(BattleResultPhase.ResultEntry entry, float y)
    {
        float centerX = Screen.width / 2f;

        // プレイヤー番号
        string playerLabel = $"P{entry.playerIndex + 1}";
        DrawText(playerLabel, centerX - 200, y, pixelScale, entry.playerColor);

        // カード名
        DrawText(entry.cardName, centerX - 100, y, pixelScale, Color.white);

        // 結果
        string resultText;
        Color resultColor;
        if (entry.success)
        {
            if (entry.category == CardCategory.Attack)
            {
                resultText = $"-{entry.damage}";
                resultColor = new Color(1f, 0.4f, 0.4f);
            }
            else if (entry.category == CardCategory.Heal)
            {
                resultText = $"+{entry.healAmount}";
                resultColor = new Color(0.4f, 1f, 0.5f);
            }
            else
            {
                resultText = "OK";
                resultColor = Color.cyan;
            }
        }
        else
        {
            resultText = "MISS";
            resultColor = Color.gray;
        }
        DrawText(resultText, centerX + 100, y, pixelScale + 1, resultColor);
    }

    private void DrawCenteredText(string text, float y, int scale, Color color)
    {
        var tex = GetOrCreateTexture(text, color, scale);
        if (tex != null)
        {
            float x = (Screen.width - tex.width) / 2f;
            GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
        }
    }

    private void DrawText(string text, float x, float y, int scale, Color color)
    {
        var tex = GetOrCreateTexture(text, color, scale);
        if (tex != null)
        {
            GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
        }
    }

    private Texture2D GetOrCreateTexture(string text, Color color, int scale)
    {
        string key = $"{text}_{color.r:F2}_{color.g:F2}_{color.b:F2}_{scale}";
        if (!textureCache.TryGetValue(key, out var tex) || tex == null)
        {
            tex = PixelFontRenderer.RenderText(text, color, scale);
            textureCache[key] = tex;
        }
        return tex;
    }
}
