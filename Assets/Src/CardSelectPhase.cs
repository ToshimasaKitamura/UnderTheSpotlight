using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// カード選択フェーズ(1画面ホットシート方式)。
///
/// 流れ:
///   1. ターン順を決定(固定 or ランダム)
///   2. ターン順に1人ずつ:
///        - そのプレイヤーの PlayerDeck から drawCount 枚ドロー
///        - 制限時間内に CardHandView で1枚選択
///        - タイムアウト時はカーソル位置(既定先頭)を自動確定
///   3. 全員選択し終えたら選択結果を保持して ActionCommandPhase へ
///
/// 結果は Selections[playerIndex] で参照可能(次フェーズが使用)。
/// 数値(人数, 制限時間, ドロー枚数, デッキ)は Inspector / アセットで調整。
/// </summary>
public class CardSelectPhase : MonoBehaviour, IBattlePhase
{
    [Header("依存")]
    [Tooltip("確定時に次フェーズへ遷移。未設定時は親から探索。")]
    [SerializeField] private BattleStateMachine stateMachine;
    [Tooltip("パーティの各デッキ。未設定時はシーンから探索。")]
    [SerializeField] private PartyDeckController partyDeck;
    [Tooltip("各プレイヤーのキャラ Transform(注目演出の対象)。playerColors/turn と同じ並び。")]
    [SerializeField] private Transform[] playerTransforms;
    [Tooltip("数値の唯一の真実(Excel/CSV連動)。設定すると下記既定値を上書き。")]
    [SerializeField] private GameTuning tuning;

    [Header("選択設定")]
    [Tooltip("プレイヤー人数。")]
    [Min(1)] public int playerCount = 4;
    [Tooltip("1人あたりの選択制限時間(秒)。0以下で無制限。")]
    public float selectTimeLimit = 15f;
    [Tooltip("ターン順をランダム化する(false なら 0,1,2,3 固定)。")]
    public bool randomizeTurnOrder = false;
    [Tooltip("各プレイヤーの色(見出し表示用)。")]
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.35f, 0.35f, 1f),
        new Color(0.35f, 0.6f, 1f, 1f),
        new Color(0.35f, 1f, 0.4f, 1f),
        new Color(1f, 0.85f, 0.35f, 1f),
    };

    /// <summary>確定済み選択(playerIndex → CardInstance)。未選択は null。</summary>
    public IReadOnlyList<CardInstance> Selections => selections;
    /// <summary>全員の選択が完了したときに発火。</summary>
    public event Action<IReadOnlyList<CardInstance>> AllSelected;

    // カード選択中は Enter を自前で消費し、全員確定後に自分で遷移する。
    public bool BlocksDebugAdvance => true;

    private CardHandView handView;
    private List<CardInstance> currentHand;
    private int[] turnOrder;
    private int turnIndex;        // turnOrder のインデックス
    private int activePlayer;     // 現在選択中のプレイヤー番号
    private CardInstance[] selections;
    private float timeLeft;
    private bool turnActive;
    private CardSelectStage stage;

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (partyDeck == null) partyDeck = FindObjectOfType<PartyDeckController>();
        ApplyTuning();
    }

    private void ApplyTuning()
    {
        if (tuning == null) return;
        playerCount = tuning.playerCount;
        selectTimeLimit = tuning.selectTimeLimit;
        randomizeTurnOrder = tuning.randomizeTurnOrder;
        var cols = tuning.ResolvePlayerColors();
        if (cols != null && cols.Length > 0) playerColors = cols;
    }

    void Start()
    {
        enabled = false;
    }

    public void Enter()
    {
        enabled = true;

        if (partyDeck == null)
        {
            UnityEngine.Debug.LogWarning("[CardSelectPhase] PartyDeckController が見つかりません。");
            return;
        }

        int n = Mathf.Max(1, playerCount);
        selections = new CardInstance[n];
        turnOrder = BuildTurnOrder(n);
        turnIndex = 0;

        SpawnStage();
        BeginPlayerTurn();
    }

    public void Exit()
    {
        enabled = false;
        turnActive = false;
        DestroyHandView();
        DestroyStage();
        UnityEngine.Debug.Log("[CardSelectPhase] カード選択フェーズ終了");
    }

    private void SpawnStage()
    {
        DestroyStage();
        if (playerTransforms == null || playerTransforms.Length == 0) return;
        var stageObj = new GameObject("CardSelectStage");
        stage = stageObj.AddComponent<CardSelectStage>();
        stage.Initialize(playerTransforms);
    }

    private void DestroyStage()
    {
        if (stage != null)
        {
            stage.Restore();
            Destroy(stage.gameObject);
            stage = null;
        }
    }

    public void Tick()
    {
        if (!turnActive || handView == null) return;

        // タイマー
        if (selectTimeLimit > 0f && !handView.IsLocked)
        {
            timeLeft -= Time.deltaTime;
            handView.SetTimer(timeLeft / selectTimeLimit);
            if (timeLeft <= 0f)
            {
                handView.Confirm(); // カーソル位置を自動確定
                return;
            }
        }

        if (handView.IsLocked) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // 縦並び: 上=前(index小) / 下=次(index大)。左右も同義で受ける。
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame
         || kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            handView.MoveCursor(-1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame
              || kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            handView.MoveCursor(+1);
        else if (kb.enterKey.wasPressedThisFrame
              || kb.numpadEnterKey.wasPressedThisFrame
              || kb.spaceKey.wasPressedThisFrame
              || kb.zKey.wasPressedThisFrame)
            handView.Confirm();
    }

    private int[] BuildTurnOrder(int n)
    {
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        if (randomizeTurnOrder)
        {
            var rng = new System.Random();
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }
        return order;
    }

    private void BeginPlayerTurn()
    {
        activePlayer = turnOrder[turnIndex];
        var deck = partyDeck.GetDeck(activePlayer);
        if (deck == null)
        {
            UnityEngine.Debug.LogWarning($"[CardSelectPhase] P{activePlayer} のデッキがありません。スキップ。");
            AdvanceTurn();
            return;
        }

        var drawn = deck.DrawHand(partyDeck.drawCount);
        currentHand = new List<CardInstance>(drawn);

        // 該当キャラに注目(スポットライト + 拡大 + 他を暗転)。アニメ中もカードは出る。
        if (stage != null) stage.FocusPlayer(activePlayer);

        SpawnHandView(currentHand);
        Color col = (playerColors != null && activePlayer < playerColors.Length)
            ? playerColors[activePlayer] : Color.white;
        handView.SetHeader($"{activePlayer + 1}番さん 選んでください", col);

        timeLeft = selectTimeLimit;
        handView.SetTimer(1f);
        turnActive = true;

        UnityEngine.Debug.Log($"[CardSelectPhase] P{activePlayer + 1} の手番 / 手札{currentHand.Count} / 強化Lv{deck.UpgradeLevel}");
    }

    private void HandleCardConfirmed(int index)
    {
        turnActive = false;
        if (currentHand != null && index >= 0 && index < currentHand.Count)
        {
            selections[activePlayer] = currentHand[index];
            UnityEngine.Debug.Log($"[CardSelectPhase] P{activePlayer + 1} 確定: {currentHand[index].Data.displayName}");
        }
        partyDeck.GetDeck(activePlayer)?.DiscardHand();
        if (stage != null) stage.PlayReaction(activePlayer);
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        turnIndex++;
        if (turnIndex >= turnOrder.Length)
        {
            FinishSelection();
        }
        else
        {
            BeginPlayerTurn();
        }
    }

    private void FinishSelection()
    {
        DestroyHandView();
        UnityEngine.Debug.Log("[CardSelectPhase] 全員選択完了 → PartSelect へ");
        AllSelected?.Invoke(selections);
        if (stateMachine != null)
        {
            stateMachine.ChangeState(BattleStateMachine.State.PartSelect);
        }
    }

    private void SpawnHandView(List<CardInstance> hand)
    {
        DestroyHandView();
        var viewObj = new GameObject("CardHandView");
        handView = viewObj.AddComponent<CardHandView>();
        handView.CardConfirmed += HandleCardConfirmed;
        handView.Build(hand);
    }

    private void DestroyHandView()
    {
        if (handView != null)
        {
            handView.CardConfirmed -= HandleCardConfirmed;
            Destroy(handView.gameObject);
            handView = null;
        }
    }
}
