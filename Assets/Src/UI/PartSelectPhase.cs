using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 部位選択フェーズ。CardSelect の直後・ActionCommand の直前に挟まる。
/// 攻撃カテゴリかつ attackRange != All のカードを選んだプレイヤーに対し、ホットシート方式で部位を選ばせる。
/// 結果は <see cref="TargetPartIds"/> に格納され、ActionCommandPhase が読む。
///
/// スキップ条件(自動で次の人へ):
///  - カードが null
///  - カードカテゴリが Attack 以外(部位を持たない=null)
///  - attackRange == All(全部位対象 → 単一 partId は null。AC 側でループ)
///  - 選択可能な部位が0(全破壊済み 等)
///  - 選択可能な部位が1(自動確定)
/// </summary>
public class PartSelectPhase : MonoBehaviour, IBattlePhase
{
    [Header("依存(未設定なら自動探索)")]
    [SerializeField] private BattleStateMachine stateMachine;
    [SerializeField] private CardSelectPhase cardSelectPhase;
    [SerializeField] private EnemyRuntimeState enemyState;
    [SerializeField] private DodgePhase dodgePhase;
    [SerializeField] private GameTuning tuning;
    [Tooltip("シーンに最初から配置されている Enemy GameObject (Visual 子に SpriteRenderer を持つもの)。未指定なら GameObject.Find(\"Enemy\")。")]
    [SerializeField] private Transform enemyTransform;

    [Header("設定")]
    [Min(1)] public int playerCount = 4;
    [Tooltip("1人あたりの部位選択制限時間(秒)。0以下で無制限。")]
    public float selectTimeLimit = 12f;
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.72f, 0.72f, 1f),   // 薄い赤
        new Color(0.72f, 0.83f, 1f, 1f),   // 薄い青
        new Color(0.72f, 1f, 0.72f, 1f),   // 薄い緑
        new Color(1f, 0.93f, 0.72f, 1f),   // 薄い黄
    };

    /// <summary>各プレイヤーが選んだ部位 ID。null=部位選択不要(非攻撃 / All / スキップ)。</summary>
    public IReadOnlyList<string> TargetPartIds => targetPartIds;

    public bool BlocksDebugAdvance => true;

    private string[] targetPartIds;
    private int activePlayer;
    private List<EnemyPart> currentParts;
    private PartSelectView view;

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (cardSelectPhase == null) cardSelectPhase = FindObjectOfType<CardSelectPhase>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (dodgePhase == null) dodgePhase = FindObjectOfType<DodgePhase>();
        if (enemyTransform == null)
        {
            var go = GameObject.Find("Enemy");
            if (go != null) enemyTransform = go.transform;
        }
        ApplyTuning();
    }

    private void ApplyTuning()
    {
        if (tuning == null) return;
        playerCount = tuning.playerCount;
        // CardSelect と同じ枠の制限時間を流用する(部位選択の長さは CSV 側で別管理にしてもよい)
        if (tuning.selectTimeLimit > 0f) selectTimeLimit = tuning.selectTimeLimit;
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

        int n = Mathf.Max(1, playerCount);
        targetPartIds = new string[n];
        activePlayer = -1;
        // 今ターンの「敵の予告部位」を1つ選び、全プレイヤーの選択画面で同じセリフを表示する
        if (enemyState != null) enemyState.PickNextTelegraph(null);
        AdvanceTurn();
    }

    public void Exit()
    {
        enabled = false;
        DestroyView();
        UnityEngine.Debug.Log("[PartSelectPhase] 終了");
    }

    public void Tick()
    {
        if (view == null) return;
        view.Tick(Time.deltaTime);
        var kb = Keyboard.current;
        if (kb == null) return;
        // 縦並びリスト時代は↑↓だったが、ヒットボックス UI では空間配置(縦/横) なので両方受け付ける
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame
            || kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) view.MoveCursor(-1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame
            || kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) view.MoveCursor(+1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                 || kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame) view.Confirm();
    }

    private void AdvanceTurn()
    {
        activePlayer++;
        while (activePlayer < targetPartIds.Length)
        {
            if (TryBeginPlayerTurn(activePlayer)) return;
            activePlayer++;
        }
        Finish();
    }

    /// <summary>true を返すと、その人の選択 UI が開いた状態で待つ。false なら次の人へ。</summary>
    private bool TryBeginPlayerTurn(int playerIndex)
    {
        var selections = cardSelectPhase != null ? cardSelectPhase.Selections : null;
        var card = (selections != null && playerIndex < selections.Count) ? selections[playerIndex] : null;
        if (card == null)
        {
            targetPartIds[playerIndex] = null;
            return false;
        }
        var data = card.Data;
        if (data == null)
        {
            targetPartIds[playerIndex] = null;
            return false;
        }
        // 【仕様】どのカードでも部位を選ばせる(非攻撃/全体攻撃も含む)。攻撃はデフォルトで行うため。
        if (enemyState == null || enemyState.enemyData == null)
        {
            UnityEngine.Debug.LogWarning("[PartSelectPhase] EnemyRuntimeState/EnemyData が見つかりません。スキップ。");
            targetPartIds[playerIndex] = null;
            return false;
        }

        // 生存している全部位を候補にする(カードの攻撃範囲に依らず、どれでも狙える)
        currentParts = new List<EnemyPart>();
        foreach (var p in enemyState.enemyData.parts)
            if (p != null && !enemyState.IsDestroyed(p.id)) currentParts.Add(p);

        if (currentParts.Count == 0)
        {
            targetPartIds[playerIndex] = null;
            return false;
        }
        if (currentParts.Count == 1)
        {
            targetPartIds[playerIndex] = currentParts[0].id;
            return false;
        }

        SpawnView(playerIndex, data);
        return true;
    }

    private void SpawnView(int playerIndex, CardData card)
    {
        DestroyView();
        var go = new GameObject("PartSelectView");
        view = go.AddComponent<PartSelectView>();
        view.PartConfirmed += OnPartConfirmed;
        view.Build(currentParts, enemyState, ResolveEnemyVisualTransform());
        Color col = (playerColors != null && playerIndex < playerColors.Length)
            ? playerColors[playerIndex] : Color.white;
        string rangeJa = card.attackRange == CardAttackRange.Upper ? "上半身"
            : card.attackRange == CardAttackRange.Lower ? "下半身"
            : "単体";
        view.SetHeader($"{playerIndex + 1}番さん [{card.displayName}] → {rangeJa} のどこを狙う?", col);
        view.SetTimeLimit(selectTimeLimit);
    }

    /// <summary>
    /// シーンに最初から置かれている Enemy GameObject の "Visual" 子(SpriteRenderer を持つ)を返す。
    /// 見つからなければ Enemy ルートを返し、それも無ければ null。
    /// PartSelectView はこれの子に world-space で部位ヒットボックスを生成する。
    /// </summary>
    private Transform ResolveEnemyVisualTransform()
    {
        if (enemyTransform == null) return null;
        var visual = enemyTransform.Find("Visual");
        return visual != null ? visual : enemyTransform;
    }

    private void OnPartConfirmed(int index)
    {
        if (currentParts == null || index < 0 || index >= currentParts.Count) return;
        targetPartIds[activePlayer] = currentParts[index].id;
        UnityEngine.Debug.Log($"[PartSelectPhase] P{activePlayer + 1} 部位確定: {currentParts[index].displayName}");
        DestroyView();
        AdvanceTurn();
    }

    private void DestroyView()
    {
        if (view != null)
        {
            view.PartConfirmed -= OnPartConfirmed;
            Destroy(view.gameObject);
            view = null;
        }
    }

    private void Finish()
    {
        UnityEngine.Debug.Log("[PartSelectPhase] 全員終了 → ActionCommand へ");
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.ActionCommand);
    }
}
