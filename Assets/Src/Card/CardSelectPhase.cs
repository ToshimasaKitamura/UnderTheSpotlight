using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// カード選択フェーズ(1画面ホットシート方式)。
///
/// 演出の流れ:
///   1. 全てのフレームが左端に集合
///   2. 対象キャラクターが左側に展開、右3つにカード3枚が表示
///   3. カード選択後、再び左に集合
///   4. 次のプレイヤーに繰り返し
///   5. 全員完了後、次のフェーズへ
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

    /// <summary>プレイヤー順(P1..P4)に並んだキャラ Transform。他フェーズが順序の真実として参照。</summary>
    public Transform[] PlayerTransforms => playerTransforms;
    [Tooltip("数値の唯一の真実(Excel/CSV連動)。設定すると下記既定値を上書き。")]
    [SerializeField] private GameTuning tuning;
    [Tooltip("バトルフレームUI")]
    [SerializeField] private BattleFrameUI frameUI;

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
        new Color(1f, 0.72f, 0.72f, 1f),   // 薄い赤
        new Color(0.72f, 0.83f, 1f, 1f),   // 薄い青
        new Color(0.72f, 1f, 0.72f, 1f),   // 薄い緑
        new Color(1f, 0.93f, 0.72f, 1f),   // 薄い黄
    };

    [Header("演出設定")]
    [Tooltip("カード登場アニメーション時間")]
    [SerializeField] private float cardEnterDuration = 0.3f;
    [Tooltip("カード登場間隔")]
    [SerializeField] private float cardEnterStagger = 0.1f;

    // ===== ズーム/カード配置チューニング =====
    // 注意: これらは意図的に [SerializeField] にしていない。
    // シリアライズするとシーン保存値がコード変更より優先され、
    // ドメインリロードをまたいで旧値が固着して初期値変更が反映されなくなるため、
    // コードを唯一の真実として通常の private フィールドで持つ。

    // カメラを各キャラへ寄せる/戻す秒数
    private float zoomDuration = 0.6f;
    // ズーム率。小さいほど大きく寄る(0.4で約2.5倍)
    private float zoomFactor = 0.4f;
    // 手番キャラの画面横位置(0=左端,1=右端)。左端まで寄せて右をカード用に空ける
    private float activeScreenX = 0.16f;
    // 手番キャラの画面縦位置(0=下,1=上)。小さいほど下に映る
    private float activeScreenY = 0.42f;
    // 手番キャラの追加拡大率(カメラズームに上乗せ)
    private float focusScaleMul = 1.15f;
    // 非手番キャラの暗転倍率(0=黒,1=元色)。キャラは消さず暗めにして手番を強調
    private float dimMultiplier = 0.5f;

    // カード列の中心の画面横位置(0.5=中央 → 3枚の2枚目がちょうど画面中央に来る)
    private float cardsCenterX = 0.5f;
    // カード列の画面縦位置(0.5=画面中央)
    private float cardsCenterY = 0.5f;
    // カード間の画面横間隔(ビューポート比)。カード幅より小さいと重なる
    private float cardSpacingViewport = 0.23f;
    // カード高さの画面縦比率(大きめ)
    private float cardHeightViewport = 0.44f;

    // カーソル移動速度(ビューポート/秒)
    private float cursorSpeed = 0.7f;
    // カーソルの大きさ(Canvas基準ピクセル。CanvasScaler 1920x1080 基準)
    private float cursorPixelSize = 80f;

    /// <summary>確定済み選択(playerIndex → CardInstance)。未選択は null。</summary>
    public IReadOnlyList<CardInstance> Selections => selections;
    /// <summary>全員の選択が完了したときに発火。</summary>
    public event Action<IReadOnlyList<CardInstance>> AllSelected;

    // カード選択中は Enter を自前で消費し、全員確定後に自分で遷移する。
    public bool BlocksDebugAdvance => true;

    private List<CardInstance> currentHand;
    private int[] turnOrder;
    private int turnIndex;        // turnOrder のインデックス
    private int activePlayer;     // 現在選択中のプレイヤー番号
    private CardInstance[] selections;
    private float timeLeft;
    private bool turnActive;
    private bool isAnimating;
    private int cursorIndex;
    private Coroutine phaseCoroutine;

    // カードUI用
    private GameObject[] cardObjects;
    private int selectedCardIndex = -1;
    private Color[][] cardOriginalColors;  // 各カードのオリジナル色を保持
    private float baseCardScale = 1f;      // カメラズームに合わせたカードの基準スケール
    private Vector3[] cardBasePositions;   // 各カードのスロット基準位置(浮遊演出の基準)
    private float cardFloatAmp = 0.02f;    // 浮遊の振幅(orthographicSize比)
    private float cardFloatSpeed = 2.6f;   // 浮遊の速さ
    private float cardSwayDeg = 1.0f;      // 浮遊時の傾き(度)
    private float hoverScaleLerp = 12f;    // ホバー拡大/縮小の追従速度(大きいほど速い)

    // 舞台演出(カメラズーム/キャラ強調)
    private CardSelectStage stage;

    // 部位選択(カード確定直後に、そのプレイヤーがボスの部位を選ぶ = 新フロー)
    private EnemyRuntimeState enemyState;
    private PartSelectPhase partSelectPhase;
    private string[] partTargets;

    // カード選択中にボスを暗くする用(部位選択で元に戻す)
    private SpriteRenderer[] bossSrs;
    private Color[] bossBaseColors;
    private const float BossDimMul = 0.32f;

    // 4Pカーソル(マウスポインタ) — Screen Space Overlay の UI として描画し、
    // ゲームカメラのズーム・画面遷移の影響を受けないようにする。残像(フェード)付き。
    private GameObject cursorCanvasObj;      // オーバーレイCanvas
    private GameObject[] playerCursors;      // 各カーソル本体(Image) 長さ4
    private Image[][] cursorGhosts;          // 各カーソルの残像Image
    private Vector2[] cursorViewportPos;     // 各カーソルのビューポート座標(0..1)
    private Vector2[][] cursorHistory;       // 残像用の位置履歴(index0 が最新)

    private const int GhostCount = 4;        // 残像の枚数
    private const int GhostStride = 2;       // 残像同士のフレーム間隔
    private const int HistoryLen = GhostCount * GhostStride + 1;
    private const float GhostMaxAlpha = 0.55f;
    // ポインタ先端(左上)をアンカー点にする pivot
    // カーソル先端 pivot は CursorSprite.TipPivot に一元化

    // 準備完了ボタン(全カード選択後、4人がカーソルで押す)
    private Vector3[] readyButtonWorldPos;         // 各ボタンのワールド中心
    private const float readyButtonRadius = 0.6f;  // 押下判定の半径(ワールド)
    private bool cursorsAllActive;                 // trueの間は全カーソルを不透明にする

    // 選択されたカード表示用
    private GameObject[] selectedCardObjects;

    // プレイヤー表示制御用
    private bool[] playerVisibilityStates;

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (partyDeck == null) partyDeck = FindObjectOfType<PartyDeckController>();
        if (frameUI == null) frameUI = FindObjectOfType<BattleFrameUI>();
        ApplyTuning();
    }

    private void ApplyTuning()
    {
        // 人数は tuning 未割り当てでも必ず反映する(選択パーティがあれば参加人数、無ければ tuning/既定)
        playerCount = BattleParty.Count(tuning);
        if (tuning == null) return;
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
        UnityEngine.Debug.Log("[CardSelectPhase] Enter 開始");
        enabled = true;

        // HPバーを非表示
        var hpGauge = FindObjectOfType<SharedHpGaugeUI>();
        if (hpGauge != null) hpGauge.SetVisible(false);

        // PartyDeckController が見つからない場合はエラー
        if (partyDeck == null)
        {
            partyDeck = FindObjectOfType<PartyDeckController>();
        }
        if (partyDeck == null)
        {
            UnityEngine.Debug.LogError("[CardSelectPhase] PartyDeckController が見つかりません！");
            return;
        }

        if (frameUI == null)
        {
            frameUI = FindObjectOfType<BattleFrameUI>();
        }
        if (frameUI == null)
        {
            UnityEngine.Debug.LogError("[CardSelectPhase] BattleFrameUI が見つかりません！");
            return;
        }

        if (Camera.main == null)
        {
            UnityEngine.Debug.LogError("[CardSelectPhase] Camera.main が見つかりません！MainCamera タグを設定してください。");
            return;
        }

        int n = Mathf.Max(1, playerCount);
        selections = new CardInstance[n];
        turnOrder = BuildTurnOrder(n);
        turnIndex = 0;

        // 部位選択(カード確定直後にインラインで実施)の準備
        partTargets = new string[n];
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (partSelectPhase == null) partSelectPhase = FindObjectOfType<PartSelectPhase>();
        // 今ターンの敵予告部位を1つ選ぶ(旧 PartSelectPhase.Enter で行っていた処理を引き継ぐ)
        if (enemyState != null) enemyState.PickNextTelegraph(null);

        UnityEngine.Debug.Log($"[CardSelectPhase] ターン順序: {string.Join(",", turnOrder)}");

        // フェーズコルーチンを開始
        phaseCoroutine = StartCoroutine(CardSelectSequence());
    }

    public void Exit()
    {
        enabled = false;
        turnActive = false;
        isAnimating = false;

        // フェーズ中断時にボスが暗いまま残らないように戻す
        RestoreBoss();

        if (phaseCoroutine != null)
        {
            StopCoroutine(phaseCoroutine);
            phaseCoroutine = null;
        }

        DestroyAllCards();
        DestroyCursors();

        // カメラ/キャラの強調演出を復帰(途中中断時のフェイルセーフ)
        if (stage != null)
        {
            stage.Restore();
        }

        // プレイヤーの表示状態を復元
        RestoreAllPlayersVisibility();

        // フレームを元の位置に戻す
        if (frameUI != null)
        {
            frameUI.RestoreAllFramesImmediate();
            frameUI.HideSpotlight();
        }

        UnityEngine.Debug.Log("[CardSelectPhase] カード選択フェーズ終了");
    }

    /// <summary>
    /// カード選択シーケンス全体
    /// </summary>
    private IEnumerator CardSelectSequence()
    {
        // カード選択フェーズ開始時に元の位置・表示状態を保存
        SaveOriginalCharacterPositions();

        // 前フェーズで分解(描画OFF)されたキャラの SpriteRenderer を戻す(このフェーズは全員から始める)
        if (playerTransforms != null)
            foreach (var pt in playerTransforms)
                if (pt != null)
                    foreach (var s in pt.GetComponentsInChildren<SpriteRenderer>(true))
                        if (s != null) s.enabled = true;

        // 舞台演出(カメラズーム/キャラ強調)を準備
        SetupStage();

        // 4P分のカーソル(マジシャンアイコン)を生成
        CreateCursors();

        // 【仕様】カード選択でも枠は消さない(手番プレイヤーだけ全体像、他は窓のまま)。

        // 各プレイヤーのターンを処理
        for (turnIndex = 0; turnIndex < turnOrder.Length; turnIndex++)
        {
            activePlayer = turnOrder[turnIndex];

            // ズームせず全体表示のまま。カード選択中はボスを暗くする(部位選択で元に戻す)。
            DarkenBoss();

            // 他プレイヤーは窓(上半身)のまま。手番プレイヤーは「顔だけ→ぴょこっとジャンプ」で全体像に。
            for (int p = 0; p < playerCount; p++)
                if (p != activePlayer) ShowPlayerWindow(p);
            yield return StartCoroutine(ShowPlayerFullAnimated(activePlayer));

            // カードをドロー
            var deck = partyDeck.GetDeck(activePlayer);
            if (deck == null)
            {
                UnityEngine.Debug.LogError($"[CardSelectPhase] P{activePlayer} のデッキがnullです！");
                continue;
            }

            var drawn = deck.DrawHand(partyDeck.drawCount);
            if (drawn == null || drawn.Count == 0)
            {
                UnityEngine.Debug.LogError($"[CardSelectPhase] P{activePlayer} のドローが空です！");
                continue;
            }
            currentHand = new List<CardInstance>(drawn);

            // カードをズーム後カメラの画面右へ生成
            yield return StartCoroutine(SpawnCardsAnimation(currentHand));

            // 選択待機。カーソルはプレイヤーが自由に動かすので、ここでは位置を動かさない
            selectedCardIndex = -1;
            cursorIndex = -1;   // ホバーで決まるため初期は未選択
            UpdateCardHighlights();
            turnActive = true;
            timeLeft = selectTimeLimit;

            UnityEngine.Debug.Log($"[CardSelectPhase] P{activePlayer + 1} の手番開始 / 手札{currentHand.Count}枚");

            // 選択が完了するまで待機
            while (selectedCardIndex < 0)
            {
                // タイムアウト処理(ホバー中ならそのカード、無ければ先頭)
                if (selectTimeLimit > 0f)
                {
                    timeLeft -= Time.deltaTime;
                    if (timeLeft <= 0f)
                    {
                        selectedCardIndex = (cursorIndex >= 0) ? cursorIndex : 0;
                    }
                }
                yield return null;
            }

            turnActive = false;

            // 選択を記録
            if (selectedCardIndex >= 0 && selectedCardIndex < currentHand.Count)
            {
                selections[activePlayer] = currentHand[selectedCardIndex];
                UnityEngine.Debug.Log($"[CardSelectPhase] P{activePlayer + 1} 確定: {currentHand[selectedCardIndex].Data.displayName}");
            }
            partyDeck.GetDeck(activePlayer)?.DiscardHand();

            // カード退場(キャラの拡大リアクションは行わない)
            yield return StartCoroutine(AnimateCardsExit(selectedCardIndex));

            // カードを消す(キャラは消さない)
            DestroyAllCards();

            // カード確定演出: 手番プレイヤーのキャラ画像を分解して弾けさせる
            yield return StartCoroutine(ExplodeCharacter(activePlayer));

            // 手番終了: 手番プレイヤーを窓(上半身)に戻す(分解済みなので非表示のまま)
            ShowPlayerWindow(activePlayer);

            // 部位選択に入るのでボスを元の色に戻す
            RestoreBoss();

            // このプレイヤーの部位選択(ボスに部位マーカーを出して狙う)
            yield return StartCoroutine(PartSelectInline(activePlayer));
        }

        // カーソルはフェーズ終了(Exit)まで残す。どの画面状態でも常に動かせるようにするため、
        // 最終演出(下からカード)中も表示・操作可能にしておく。

        // 念のためボスを元の色へ(全員終了時点で暗転が残らないように)
        RestoreBoss();

        // カメラを元に戻す。この後カードが下から出るので、キャラは画面に残さず全員非表示にする
        // (ShowSelectedCardsFromBottom の最後で RestoreAllPlayersVisibility により復帰する)
        HideAllPlayers();
        stage.Restore();
        yield return StartCoroutine(ShowSelectedCardsFromBottom());

        // 次のフェーズへ
        FinishSelection();
    }

    /// <summary>
    /// カメラズーム/キャラ強調用の CardSelectStage を用意・設定する。
    /// </summary>
    private void SetupStage()
    {
        if (stage == null) stage = GetComponent<CardSelectStage>();
        if (stage == null) stage = gameObject.AddComponent<CardSelectStage>();

        stage.targetCamera = Camera.main;
        stage.zoomFactor = zoomFactor;
        stage.activeScreenX = activeScreenX;
        stage.activeScreenY = activeScreenY;
        stage.cameraMoveDuration = zoomDuration;
        stage.focusScaleMul = focusScaleMul;
        stage.dimMultiplier = dimMultiplier;
        // 暗幕/スポットライトは使わず、ズームとキャラ強調のみ行う
        stage.disableSpotlight = true;

        // ※ 枠は消さず、キャラは窓のまま。手番プレイヤーだけ全体像にする処理は手番ごとに行う。
        stage.Initialize(playerTransforms);
    }

    /// <summary>
    /// 現在の(ズーム後)カメラ画面右に、count 枚のカードを並べるワールド座標を算出。
    /// </summary>
    private Vector3[] ComputeCardSlots(int count)
    {
        Camera cam = Camera.main;
        var slots = new Vector3[count];
        // 直交カメラでは深度は x/y に影響しないが、明示的に前方距離を渡す
        float depth = Mathf.Abs(cam.transform.position.z);

        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) / 2f) * cardSpacingViewport;
            float vx = Mathf.Clamp01(cardsCenterX + offset);
            Vector3 w = cam.ViewportToWorldPoint(new Vector3(vx, cardsCenterY, depth));
            w.z = 0f;
            slots[i] = w;
        }
        return slots;
    }

    /// <summary>
    /// 現在のカメラ倍率に対して、カード高さが cardHeightViewport になるスケールを算出。
    /// スプライトの実寸から求めるため PPU に依存しない。
    /// </summary>
    private float ComputeCardScale(GameObject cardObj)
    {
        Camera cam = Camera.main;
        var sr = cardObj.GetComponent<SpriteRenderer>();
        float spriteWorldHeight = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.y : 1f;
        if (spriteWorldHeight <= 0.0001f) spriteWorldHeight = 1f;

        float desiredWorldHeight = cardHeightViewport * (cam.orthographicSize * 2f);
        return desiredWorldHeight / spriteWorldHeight;
    }

    public void Tick()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // カーソルは画面がどんな状態でも常に動かせる
        // (通常=手番Pのカーソル / デバッグ=数字キーを押した対象Pのカーソル)
        UpdateCursors(kb);

        // カードの選択(ホバー判定・確定)は、選択受付中のみ行う
        if (!turnActive || isAnimating) return;

        // 手番プレイヤーのカーソルが乗っているカードをホバー中とする
        cursorIndex = GetActiveHoveredCard();
        UpdateCardHighlights();

        // ホバー中のカードがあり、決定キーが押されたら確定
        if (cursorIndex >= 0 && ConfirmPressed(kb))
        {
            selectedCardIndex = cursorIndex;
        }
    }

    /// <summary>
    /// 決定キー(Enter/Space)が押されたか。
    /// </summary>
    private bool ConfirmPressed(Keyboard kb)
    {
        return kb.enterKey.wasPressedThisFrame
            || kb.numpadEnterKey.wasPressedThisFrame
            || kb.spaceKey.wasPressedThisFrame;
    }

    /// <summary>
    /// カーソル移動処理。
    /// 数字キー(1〜4)を押している間は対象Pのカーソルを、押していなければ手番Pのカーソルを、
    /// 矢印/WASD で動かす。
    /// </summary>
    private void UpdateCursors(Keyboard kb)
    {
        if (playerCursors == null || cursorViewportPos == null) return;

        Vector2 dir = Vector2.zero;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir.x -= 1f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir.x += 1f;
        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dir.y += 1f;
        if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dir.y -= 1f;
        if (dir.sqrMagnitude > 1f) dir = dir.normalized;

        bool[] holdP = new bool[4];
        holdP[0] = kb.digit1Key.isPressed;
        holdP[1] = kb.digit2Key.isPressed;
        holdP[2] = kb.digit3Key.isPressed;
        holdP[3] = kb.digit4Key.isPressed;
        bool anyHeld = holdP[0] || holdP[1] || holdP[2] || holdP[3];

        if (dir != Vector2.zero)
        {
            for (int i = 0; i < playerCount; i++)
            {
                bool move = anyHeld ? holdP[i] : (i == activePlayer);
                if (!move) continue;
                cursorViewportPos[i] += dir * cursorSpeed * Time.deltaTime;
                cursorViewportPos[i].x = Mathf.Clamp01(cursorViewportPos[i].x);
                cursorViewportPos[i].y = Mathf.Clamp01(cursorViewportPos[i].y);
            }
        }

        PushCursorHistory();
        RefreshCursorTransforms();
    }

    /// <summary>
    /// 残像用に各カーソルの位置履歴を1フレーム分進める(index0 が最新)。
    /// </summary>
    private void PushCursorHistory()
    {
        if (cursorHistory == null || cursorViewportPos == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (cursorHistory[i] == null) continue;
            for (int h = HistoryLen - 1; h >= 1; h--)
            {
                cursorHistory[i][h] = cursorHistory[i][h - 1];
            }
            cursorHistory[i][0] = cursorViewportPos[i];
        }
    }

    /// <summary>
    /// 手番プレイヤーのカーソルが乗っているカードのインデックスを返す(無ければ -1)。
    /// </summary>
    private int GetActiveHoveredCard()
    {
        if (cardObjects == null || cursorViewportPos == null) return -1;
        if (activePlayer < 0 || activePlayer >= cursorViewportPos.Length) return -1;

        Camera cam = Camera.main;
        if (cam == null) return -1;

        // UIカーソルのビューポート座標を、カード(ワールド)と当たり判定するためワールドへ変換
        float depth = Mathf.Abs(cam.transform.position.z);
        Vector3 c = cam.ViewportToWorldPoint(new Vector3(
            cursorViewportPos[activePlayer].x, cursorViewportPos[activePlayer].y, depth));
        c.z = 0f;

        int hovered = -1;
        for (int i = 0; i < cardObjects.Length; i++)
        {
            if (cardObjects[i] == null) continue;
            var sr = cardObjects[i].GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            Bounds b = sr.bounds;
            if (c.x >= b.min.x && c.x <= b.max.x && c.y >= b.min.y && c.y <= b.max.y)
            {
                hovered = i;   // 重なり時は後(右)のカードを優先
            }
        }
        return hovered;
    }

    // ======= 4Pカーソル(マジシャンアイコン) =======

    /// <summary>
    /// 4人分のカーソルを生成する。Screen Space Overlay のCanvas上にUI(Image)として配置し、
    /// ゲームカメラのズーム・画面遷移の影響を受けないようにする。各プレイヤー色のマジシャン帽アイコン。
    /// </summary>
    private void CreateCursors()
    {
        DestroyCursors();

        // 最前面のオーバーレイCanvas
        cursorCanvasObj = new GameObject("PlayerCursorCanvas",
            typeof(Canvas), typeof(CanvasScaler));
        var canvas = cursorCanvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 他のUIより手前
        var scaler = cursorCanvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        playerCursors = new GameObject[4];
        cursorViewportPos = new Vector2[4];
        cursorGhosts = new Image[4][];
        cursorHistory = new Vector2[4][];

        for (int i = 0; i < playerCount; i++)
        {
            // 初期位置は画面右下に横一列で整列
            Vector2 start = new Vector2(0.78f + i * 0.055f, 0.10f);
            cursorViewportPos[i] = start;

            Sprite sprite = CursorSprite.CreatePointer(GetPlayerColor(i));

            // 残像(先に生成 = 背面に描画)
            cursorGhosts[i] = new Image[GhostCount];
            for (int g = 0; g < GhostCount; g++)
            {
                cursorGhosts[i][g] = CreateCursorImage($"Cursor_P{i + 1}_Ghost{g}", sprite);
            }

            // 本体(残像より後 = 前面に描画)
            var mainImg = CreateCursorImage($"PlayerCursor_P{i + 1}", sprite);
            playerCursors[i] = mainImg.gameObject;

            // 履歴を初期位置で埋める
            cursorHistory[i] = new Vector2[HistoryLen];
            for (int h = 0; h < HistoryLen; h++) cursorHistory[i][h] = start;
        }

        RefreshCursorTransforms();
    }

    /// <summary>
    /// カーソル用の Image(RectTransform 付き)を1枚生成する。先端を pivot に合わせる。
    /// </summary>
    private Image CreateCursorImage(string name, Sprite sprite)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(cursorCanvasObj.transform, false);

        var img = obj.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(cursorPixelSize, cursorPixelSize);
        rt.pivot = CursorSprite.TipPivot;
        return img;
    }

    /// <summary>
    /// カーソルを破棄する。
    /// </summary>
    private void DestroyCursors()
    {
        if (cursorCanvasObj != null)
        {
            Destroy(cursorCanvasObj);   // 子のカーソルUI(本体・残像)も一緒に破棄される
            cursorCanvasObj = null;
        }
        playerCursors = null;
        cursorGhosts = null;
        cursorViewportPos = null;
        cursorHistory = null;
        cursorsAllActive = false;
    }

    /// <summary>
    /// 各カーソルUI(本体+残像)のアンカー位置とアルファを反映。
    /// アンカーを正規化(0..1)で置くため、カメラや解像度に依存しない。
    /// 手番=不透明,他=半透明。残像は履歴を辿って古いほど薄くする。
    /// </summary>
    private void RefreshCursorTransforms()
    {
        if (playerCursors == null || cursorViewportPos == null) return;

        for (int i = 0; i < 4; i++)
        {
            float mainAlpha = (cursorsAllActive || i == activePlayer) ? 1f : 0.4f;

            // 残像(古いほど薄い)
            if (cursorGhosts != null && cursorGhosts[i] != null && cursorHistory != null && cursorHistory[i] != null)
            {
                for (int g = 0; g < cursorGhosts[i].Length; g++)
                {
                    var gimg = cursorGhosts[i][g];
                    if (gimg == null) continue;
                    int hi = Mathf.Min((g + 1) * GhostStride, HistoryLen - 1);
                    SetImageAnchor(gimg.rectTransform, cursorHistory[i][hi]);
                    float fade = 1f - (g + 1) / (float)(GhostCount + 1);
                    SetImageAlpha(gimg, mainAlpha * GhostMaxAlpha * fade);
                }
            }

            // 本体
            if (playerCursors[i] != null)
            {
                var rt = playerCursors[i].GetComponent<RectTransform>();
                if (rt != null) SetImageAnchor(rt, cursorViewportPos[i]);
                SetImageAlpha(playerCursors[i].GetComponent<Image>(), mainAlpha);
            }
        }
    }

    private static void SetImageAnchor(RectTransform rt, Vector2 viewport)
    {
        if (rt == null) return;
        rt.anchorMin = viewport;
        rt.anchorMax = viewport;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        img.color = new Color(c.r, c.g, c.b, alpha);
    }

    // マウスカーソル絵の生成は CursorSprite.CreatePointer に一元化(重複排除)

    private void UpdateCardHighlights()
    {
        if (cardObjects == null) return;

        float baseScale = baseCardScale;

        for (int i = 0; i < cardObjects.Length; i++)
        {
            if (cardObjects[i] == null) continue;

            // ホバー中のカードは最前面に出す(重なり時に隠れないように)
            SetCardFront(cardObjects[i], i == cursorIndex);

            // 暗い状態は無し。全カード常にフルカラー。
            // カーソルが当たったカードだけ拡大＋グロー＋浮遊でホバー表現する。
            SetCardBrightness(cardObjects[i], i, 1f);

            bool hovered = (i == cursorIndex);

            // 拡大は一瞬で切り替えず、目標へなめらかに補間(フレームレート非依存)
            Vector3 targetScale = Vector3.one * baseScale * (hovered ? 1.12f : 1f);
            float k = 1f - Mathf.Exp(-hoverScaleLerp * Time.deltaTime);
            cardObjects[i].transform.localScale = Vector3.Lerp(cardObjects[i].transform.localScale, targetScale, k);

            // 浮遊演出: ホバー中は基準位置を中心に上下へゆらゆら＋わずかに傾ける
            if (cardBasePositions != null && i < cardBasePositions.Length)
            {
                if (hovered)
                {
                    Camera cam = Camera.main;
                    float amp = (cam != null ? cam.orthographicSize : 5f) * cardFloatAmp;
                    float bob = Mathf.Sin(Time.time * cardFloatSpeed) * amp;
                    float sway = Mathf.Sin(Time.time * cardFloatSpeed * 0.7f) * cardSwayDeg;
                    cardObjects[i].transform.position = cardBasePositions[i] + new Vector3(0f, bob, 0f);
                    cardObjects[i].transform.rotation = Quaternion.Euler(0f, 0f, sway);
                }
                else
                {
                    cardObjects[i].transform.position = cardBasePositions[i];
                    cardObjects[i].transform.rotation = Quaternion.identity;
                }
            }

            var glow = cardObjects[i].transform.Find("Glow");
            if (glow != null)
            {
                var glowSr = glow.GetComponent<SpriteRenderer>();
                if (glowSr != null)
                {
                    Color c = glowSr.color;
                    float targetGlow = hovered ? 0.7f : 0f;
                    glowSr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, targetGlow, k));
                }
            }
        }
    }

    /// <summary>
    /// カードの描画順を制御。選択中カードを最前面に出し、重なっても隠れないようにする。
    /// 生成時の基準: Glow=98, 本体=100, テキスト=104。front時は +20 する。
    /// </summary>
    private void SetCardFront(GameObject cardObj, bool front)
    {
        int boost = front ? 20 : 0;

        var srs = cardObj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in srs)
        {
            int baseOrder = sr.gameObject.name == "Glow" ? 98 : 100;
            sr.sortingOrder = baseOrder + boost;
        }

        var mrs = cardObj.GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in mrs)
        {
            mr.sortingOrder = 104 + boost;
        }
    }

    private void SetCardBrightness(GameObject cardObj, int cardIndex, float brightness)
    {
        if (cardOriginalColors == null || cardIndex >= cardOriginalColors.Length) return;

        var srs = cardObj.GetComponentsInChildren<SpriteRenderer>();
        var originalColors = cardOriginalColors[cardIndex];
        if (originalColors == null) return;

        for (int i = 0; i < srs.Length && i < originalColors.Length; i++)
        {
            var sr = srs[i];
            if (sr.gameObject.name == "Glow") continue;  // グローは別処理

            Color orig = originalColors[i];
            sr.color = new Color(orig.r * brightness, orig.g * brightness, orig.b * brightness, orig.a);
        }
    }

    private void StoreCardOriginalColors(GameObject cardObj, int cardIndex)
    {
        var srs = cardObj.GetComponentsInChildren<SpriteRenderer>();
        cardOriginalColors[cardIndex] = new Color[srs.Length];

        for (int i = 0; i < srs.Length; i++)
        {
            cardOriginalColors[cardIndex][i] = srs[i].color;
        }
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

    /// <summary>
    /// カードをアニメーション付きで生成（画面右外からシャッフル演出で登場）
    /// </summary>
    private IEnumerator SpawnCardsAnimation(List<CardInstance> hand)
    {
        isAnimating = true;

        Camera cam = Camera.main;
        int count = Mathf.Min(hand.Count, 3);
        Vector3[] cardSlots = ComputeCardSlots(count);
        cardObjects = new GameObject[hand.Count];
        cardOriginalColors = new Color[hand.Count][];
        cardBasePositions = new Vector3[hand.Count];

        // 開始位置（画面右外・少し上のデッキから配られる想定 = 右からドロー）
        Vector3 deckPosition = cam.ViewportToWorldPoint(new Vector3(1.3f, cardsCenterY + 0.18f, Mathf.Abs(cam.transform.position.z)));
        deckPosition.z = 0f;

        // カードの基準スケールは最初の1枚から算出し、全カードで統一する
        baseCardScale = 1f;
        bool scaleResolved = false;

        for (int i = 0; i < count; i++)
        {
            var card = hand[i];
            Vector3 targetPos = cardSlots[i];
            cardBasePositions[i] = targetPos;   // 浮遊演出の基準位置

            // 全てのカードはデッキ位置から開始
            GameObject cardObj = CreateCardVisual(card, deckPosition);
            cardObjects[i] = cardObj;

            // 最初のカードでカメラ倍率に応じた基準スケールを確定
            if (!scaleResolved)
            {
                baseCardScale = ComputeCardScale(cardObj);
                scaleResolved = true;
            }
            cardObj.transform.localScale = Vector3.one * baseCardScale;

            // オリジナル色を保存
            StoreCardOriginalColors(cardObj, i);

            // シャッフル演出付きアニメーション開始
            StartCoroutine(AnimateCardShuffleEnter(cardObj, deckPosition, targetPos, cardEnterDuration + 0.15f, i));

            yield return new WaitForSeconds(cardEnterStagger);
        }

        // 最後のカードの登場＋めくり演出の完了を待つ
        yield return new WaitForSeconds(cardEnterDuration + 0.2f + 0.3f);

        isAnimating = false;
    }

    /// <summary>
    /// シャッフル演出付きでカードを登場させる
    /// </summary>
    private IEnumerator AnimateCardShuffleEnter(GameObject cardObj, Vector3 startPos, Vector3 endPos, float duration, int cardIndex)
    {
        float elapsed = 0f;

        // 初期回転（右手から配るように傾いた状態で始まり、着地で0へ戻る）
        float startRotation = -20f - cardIndex * 4f;
        cardObj.transform.rotation = Quaternion.Euler(0f, 0f, startRotation);

        // 中間点（弧を浅くして右から横滑りで入る感じに）
        Vector3 midPoint = (startPos + endPos) / 2f + new Vector3(0f, 0.35f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // ベジェ曲線でアーチを描く動き
            float easeT = EaseOutQuart(t);
            Vector3 p0 = startPos;
            Vector3 p1 = midPoint;
            Vector3 p2 = endPos;

            // 2次ベジェ曲線
            Vector3 currentPos = (1 - easeT) * (1 - easeT) * p0 + 2 * (1 - easeT) * easeT * p1 + easeT * easeT * p2;
            cardObj.transform.position = currentPos;

            // 回転を徐々に戻す
            float currentRotation = Mathf.Lerp(startRotation, 0f, EaseOutBack(t));
            cardObj.transform.rotation = Quaternion.Euler(0f, 0f, currentRotation);

            yield return null;
        }

        cardObj.transform.position = endPos;
        cardObj.transform.rotation = Quaternion.identity;

        // スロット到達後、裏面をめくって表を見せる
        yield return StartCoroutine(FlipReveal(cardObj));
    }

    /// <summary>
    /// カードを横に潰して裏面を消し、表を見せるめくり演出。
    /// </summary>
    private IEnumerator FlipReveal(GameObject cardObj)
    {
        if (cardObj == null) yield break;

        Transform back = cardObj.transform.Find("Back");
        if (back == null) yield break;  // 裏面が無ければ何もしない(表のまま)

        Vector3 baseScale = cardObj.transform.localScale;
        float half = 0.13f;

        // X方向に潰す(裏が見えている状態)
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / half);
            cardObj.transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z);
            yield return null;
        }

        // 一番薄いところで裏を消して表に切り替え
        back.gameObject.SetActive(false);

        // X方向に開いて表を見せる
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            cardObj.transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z);
            yield return null;
        }

        cardObj.transform.localScale = baseScale;
    }

    /// <summary>
    /// カード退場アニメーション（選択カードは下へ、他は画面右外へ）
    /// </summary>
    private IEnumerator AnimateCardsExit(int selectedIndex)
    {
        if (cardObjects == null) yield break;

        Camera cam = Camera.main;
        // 非選択カードは画面右外へ戻す
        Vector3 rightExitPos = cam.ViewportToWorldPoint(new Vector3(1.35f, cardsCenterY, Mathf.Abs(cam.transform.position.z)));
        rightExitPos.z = 0f;
        // 選択カードは現在のカメラ高さ分だけ下へ抜ける
        float downDistance = cam.orthographicSize * 2f + 2f;

        float exitDuration = 0.35f;

        // 全てのカードを同時に退場させる
        for (int i = 0; i < cardObjects.Length; i++)
        {
            if (cardObjects[i] == null) continue;

            Vector3 startPos = cardObjects[i].transform.position;

            if (i == selectedIndex)
            {
                // 選択されたカードは下へスクリーンアウト
                Vector3 targetPos = startPos + new Vector3(0f, -downDistance, 0f);
                StartCoroutine(AnimateCardExitDown(cardObjects[i], startPos, targetPos, exitDuration));
            }
            else
            {
                // 他のカードは画面右外へ戻る
                StartCoroutine(AnimateCardExitLeft(cardObjects[i], startPos, rightExitPos, exitDuration, i));
            }
        }

        yield return new WaitForSeconds(exitDuration + 0.1f);
    }

    /// <summary>
    /// 選択カードを下へスクリーンアウト
    /// </summary>
    private IEnumerator AnimateCardExitDown(GameObject cardObj, Vector3 startPos, Vector3 endPos, float duration)
    {
        float elapsed = 0f;
        Vector3 startScale = cardObj.transform.localScale;
        Vector3 endScale = startScale * 1.2f;  // 少し拡大しながら

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseInQuad(t);

            cardObj.transform.position = Vector3.Lerp(startPos, endPos, easeT);
            cardObj.transform.localScale = Vector3.Lerp(startScale, endScale, easeT * 0.5f);

            // フェードアウト
            var srs = cardObj.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in srs)
            {
                Color c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, 1f - easeT);
            }

            yield return null;
        }
    }

    /// <summary>
    /// 非選択カードを左へ戻す
    /// </summary>
    private IEnumerator AnimateCardExitLeft(GameObject cardObj, Vector3 startPos, Vector3 endPos, float duration, int cardIndex)
    {
        float elapsed = 0f;
        float rotationTarget = -20f + cardIndex * 8f;  // シャッフル風に傾ける

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseInQuart(t);

            cardObj.transform.position = Vector3.Lerp(startPos, endPos, easeT);
            cardObj.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, rotationTarget, easeT));

            yield return null;
        }
    }

    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }

    private float EaseInQuad(float t)
    {
        return t * t;
    }

    private float EaseInQuart(float t)
    {
        return t * t * t * t;
    }

    /// <summary>
    /// テキストを指定文字数で改行する
    /// </summary>
    private string WrapText(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxCharsPerLine)
        {
            return text;
        }

        var sb = new System.Text.StringBuilder();
        int charCount = 0;

        foreach (char c in text)
        {
            sb.Append(c);
            charCount++;

            if (charCount >= maxCharsPerLine)
            {
                sb.Append('\n');
                charCount = 0;
            }
        }

        return sb.ToString();
    }

    private GameObject CreateCardVisual(CardInstance card, Vector3 position)
    {
        GameObject cardObj = new GameObject($"Card_{card.Data.displayName}");
        cardObj.transform.position = position;

        // スケールは呼び出し側でカメラ倍率に応じて設定する（ここでは等倍）
        cardObj.transform.localScale = Vector3.one;

        // 洗練されたTCGスタイルのカードSpriteを取得
        Sprite cardSprite = CardVisualGenerator.GetCardSprite(card);

        // カード本体（スプライト画像）
        var bgSr = cardObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = cardSprite;
        bgSr.sortingOrder = 100;

        // グロー効果（ホバー・選択時に使用）
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(cardObj.transform);
        glowObj.transform.localPosition = Vector3.zero;
        var glowSr = glowObj.AddComponent<SpriteRenderer>();
        glowSr.sprite = cardSprite;
        Color glowColor = CardVisualGenerator.GetCategoryColor(card.Data.category);
        // 最初はグロー(外枠)を消した状態で配る。ホバー時のみ点灯する。
        glowSr.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        glowSr.sortingOrder = 98;
        glowObj.transform.localScale = Vector3.one * 1.08f;

        // カード裏面（配布時は裏、スロット到達でめくって表にする）
        // ※ 最後の子として追加する。表を隠すため最前面。SpriteRenderer列の末尾になるよう最後に生成。
        Sprite backSprite = CardVisualGenerator.GetCardBackSprite();
        if (backSprite != null)
        {
            GameObject backObj = new GameObject("Back");
            backObj.transform.SetParent(cardObj.transform);
            backObj.transform.localPosition = Vector3.zero;

            var backSr = backObj.AddComponent<SpriteRenderer>();
            backSr.sprite = backSprite;
            backSr.sortingOrder = 110;  // 表(bg=100)より前面で覆う

            Vector3 frontSize = bgSr.sprite != null ? bgSr.sprite.bounds.size : Vector3.one;
            Vector3 backSize = backSprite.bounds.size;
            backObj.transform.localScale = new Vector3(
                backSize.x > 0.0001f ? frontSize.x / backSize.x : 1f,
                backSize.y > 0.0001f ? frontSize.y / backSize.y : 1f,
                1f);
        }

        return cardObj;
    }

    /// <summary>
    /// カテゴリとティアに応じたカードスプライトのパスを取得
    /// </summary>
    private string GetCardSpritePath(CardCategory category, CardTier tier)
    {
        // Resources/Cards/ フォルダに配置する想定
        // 例: Cards/Card_Attack, Cards/Card_Defend, Cards/Card_Ultimate
        string categoryName = category.ToString();

        // 必殺カード（Ultimate）は専用のスプライトを使用
        if (tier == CardTier.Ultimate)
        {
            return "Cards/Card_Ultimate";
        }

        return $"Cards/Card_{categoryName}";
    }

    /// <summary>
    /// スプライトが見つからない場合のフォールバック
    /// </summary>
    private Sprite CreateFallbackCardSprite()
    {
        int width = 64;
        int height = 96;
        int cornerRadius = 6;

        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inCorner = false;

                if (x < cornerRadius && y < cornerRadius)
                    inCorner = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius)) > cornerRadius;
                else if (x >= width - cornerRadius && y < cornerRadius)
                    inCorner = Vector2.Distance(new Vector2(x, y), new Vector2(width - cornerRadius - 1, cornerRadius)) > cornerRadius;
                else if (x < cornerRadius && y >= height - cornerRadius)
                    inCorner = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, height - cornerRadius - 1)) > cornerRadius;
                else if (x >= width - cornerRadius && y >= height - cornerRadius)
                    inCorner = Vector2.Distance(new Vector2(x, y), new Vector2(width - cornerRadius - 1, height - cornerRadius - 1)) > cornerRadius;

                if (inCorner)
                    pixels[y * width + x] = Color.clear;
                else
                    pixels[y * width + x] = Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 64f);
    }

    private Color GetCardColor(CardCategory category)
    {
        switch (category)
        {
            case CardCategory.Attack:
                return new Color(0.7f, 0.2f, 0.2f, 1f);   // 赤
            case CardCategory.Defend:
                return new Color(0.2f, 0.4f, 0.8f, 1f);   // 青
            case CardCategory.Heal:
                return new Color(0.2f, 0.7f, 0.3f, 1f);   // 緑
            case CardCategory.Buff:
                return new Color(0.85f, 0.65f, 0.1f, 1f); // 黄
            case CardCategory.Special:
                return new Color(0.55f, 0.25f, 0.75f, 1f);// 紫
            default:
                return new Color(0.4f, 0.4f, 0.5f, 1f);
        }
    }

    private void DestroyAllCards()
    {
        if (cardObjects == null) return;

        foreach (var cardObj in cardObjects)
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }
        cardObjects = null;
        cardBasePositions = null;
    }

    /// <summary>
    /// キャラクターの元の位置と表示状態を保存
    /// </summary>
    private void SaveOriginalCharacterPositions()
    {
        if (playerTransforms == null || playerTransforms.Length == 0) return;

        // 元の状態を保存
        playerVisibilityStates = new bool[playerTransforms.Length];

        // 元の位置も保存
        if (originalCharacterPositions == null)
        {
            originalCharacterPositions = new Vector3[playerTransforms.Length];
        }

        for (int i = 0; i < playerTransforms.Length; i++)
        {
            if (playerTransforms[i] == null) continue;
            playerVisibilityStates[i] = playerTransforms[i].gameObject.activeSelf;
            originalCharacterPositions[i] = playerTransforms[i].position;
        }
    }

    /// <summary>
    /// 全プレイヤーを非表示にする
    /// </summary>
    private void HideAllPlayers()
    {
        if (playerTransforms == null || playerTransforms.Length == 0) return;

        for (int i = 0; i < playerTransforms.Length; i++)
        {
            if (playerTransforms[i] == null) continue;
            playerTransforms[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 対象プレイヤーのみを表示する
    /// </summary>
    private void ShowOnlyActivePlayer(int activePlayerIndex)
    {
        if (playerTransforms == null || playerTransforms.Length == 0) return;

        for (int i = 0; i < playerTransforms.Length; i++)
        {
            if (playerTransforms[i] == null) continue;

            if (i == activePlayerIndex)
            {
                playerTransforms[i].gameObject.SetActive(true);
            }
            else
            {
                playerTransforms[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 指定プレイヤーを非表示にする
    /// </summary>
    private void HidePlayer(int playerIndex)
    {
        if (playerTransforms == null || playerIndex >= playerTransforms.Length) return;
        if (playerTransforms[playerIndex] != null)
        {
            playerTransforms[playerIndex].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 全プレイヤーの表示状態を復元
    /// </summary>
    private void RestoreAllPlayersVisibility()
    {
        if (playerTransforms == null || playerVisibilityStates == null) return;

        for (int i = 0; i < playerTransforms.Length; i++)
        {
            if (playerTransforms[i] == null) continue;

            if (i < playerVisibilityStates.Length)
            {
                playerTransforms[i].gameObject.SetActive(playerVisibilityStates[i]);
            }
            else
            {
                playerTransforms[i].gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// プレイヤーのTransformを取得
    /// </summary>
    private Transform GetPlayerTransform(int playerIndex)
    {
        if (playerTransforms == null || playerIndex >= playerTransforms.Length)
        {
            return null;
        }
        return playerTransforms[playerIndex];
    }

    /// <summary>
    /// カード確定直後の部位選択(新フロー)。そのプレイヤーがボスの生存部位を1つ狙う。
    /// 結果は <see cref="partTargets"/>[player] に格納し、最後に PartSelectPhase へ流し込む。
    /// </summary>
    private IEnumerator PartSelectInline(int player)
    {
        // 生存している部位を候補にする
        var parts = new List<EnemyPart>();
        if (enemyState != null && enemyState.enemyData != null && enemyState.enemyData.parts != null)
        {
            foreach (var p in enemyState.enemyData.parts)
                if (p != null && !enemyState.IsDestroyed(p.id)) parts.Add(p);
        }

        // 候補0=スキップ / 候補1=自動確定(UIは出さない)
        if (parts.Count == 0) { partTargets[player] = null; yield break; }
        if (parts.Count == 1) { partTargets[player] = parts[0].id; yield break; }

        // ボスを画面に収める(カメラを全体へ戻す。キャラ強調は維持)
        if (stage != null) yield return StartCoroutine(stage.MoveCameraHome(0.35f));

        // カード用の4Pカーソルは部位選択中は隠す
        if (cursorCanvasObj != null) cursorCanvasObj.SetActive(false);

        var go = new GameObject("InlinePartSelectView");
        var view = go.AddComponent<PartSelectView>();
        int chosen = -1;
        System.Action<int> onConfirm = idx => chosen = idx;
        view.PartConfirmed += onConfirm;
        view.Build(parts, enemyState, ResolveEnemyVisual());

        Color col = (GameColorScheme.Players != null && player < GameColorScheme.Players.Length)
            ? GameColorScheme.Players[player] : Color.white;
        view.SetHeader($"{player + 1}P: どこを狙う?", col);
        view.SetTimeLimit(selectTimeLimit);

        // アクティブプレイヤーがレティクル(照準)で狙う。
        // スティック / WASD / 矢印で自由移動、重なった(最も近い)部位がハイライト、Enter系/Aボタンで確定。
        const float pointerSpeed = 1.1f; // 正規化座標/秒(画面全体を約1秒弱で横断)
        while (chosen < 0)
        {
            view.Tick(Time.deltaTime);
            if (!view.IsLocked)
            {
                var kb = Keyboard.current;
                var gp = Gamepad.current;
                Vector2 move = Vector2.zero;
                if (kb != null)
                {
                    if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) move.x -= 1f;
                    if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) move.x += 1f;
                    if (kb.upArrowKey.isPressed || kb.wKey.isPressed) move.y += 1f;
                    if (kb.downArrowKey.isPressed || kb.sKey.isPressed) move.y -= 1f;
                }
                if (gp != null) move += gp.leftStick.ReadValue();
                if (move.sqrMagnitude > 1f) move = move.normalized;
                if (move != Vector2.zero) view.MovePointer(move * pointerSpeed * Time.deltaTime);

                bool confirm =
                    (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                        || kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame))
                    || (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame));
                if (confirm) view.Confirm();
            }
            yield return null;
        }

        view.PartConfirmed -= onConfirm;
        if (chosen >= 0 && chosen < parts.Count) partTargets[player] = parts[chosen].id;
        view.Clear();   // 敵に貼った部位マーカー(enemyVisual の子)を明示的に破棄(GO 破棄では消えないため)
        Destroy(go);

        if (cursorCanvasObj != null) cursorCanvasObj.SetActive(true);
    }

    /// <summary>
    /// 手番プレイヤーを「顔だけ(窓)→ ぴょこっとジャンプ → 全体像」で見せる。
    /// クリップ解除して全身を出し、上へ跳ねてから枠中心・枠フィットへ着地(軽くオーバーシュート)。
    /// </summary>
    private IEnumerator ShowPlayerFullAnimated(int p)
    {
        if (frameUI == null || playerTransforms == null || p < 0 || p >= playerTransforms.Length) yield break;
        var t = playerTransforms[p];
        if (t == null) yield break;

        Vector3 startPos = t.position;
        Vector3 startScale = t.localScale;

        Vector3 endPos = frameUI.GetAllyFrameCenter(p);
        Vector3 endScale = startScale;
        var sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float sc = frameUI.CalculateAllyScale(sr.sprite, p);
            endScale = new Vector3(sc, sc, 1f);
        }

        // クリップ解除して全身を見せる(ジャンプで飛び出す)
        frameUI.SetPlayerWindowClip(p, false);

        const float dur = 0.34f;
        const float jumpH = 0.8f; // 上へ跳ねる高さ(ワールド)
        float el = 0f;
        while (el < dur)
        {
            el += Time.deltaTime;
            float k = Mathf.Clamp01(el / dur);
            Vector3 pos = Vector3.Lerp(startPos, endPos, 1f - (1f - k) * (1f - k)); // EaseOutQuad
            pos.y += Mathf.Sin(k * Mathf.PI) * jumpH;                                 // ぴょこっと山なり
            t.position = pos;
            t.localScale = Vector3.LerpUnclamped(startScale, endScale, EaseOutBack(k)); // 軽いオーバーシュート
            yield return null;
        }
        t.position = endPos;
        t.localScale = endScale;
    }

    /// <summary>
    /// カード確定演出: 手番プレイヤーのキャラ画像を格子状の破片に分解して弾けさせる。
    /// スプライトのサブ矩形で破片を作り、外向き＋上向きに飛ばして重力・回転・フェード。
    /// 破片が消えたら元キャラは非表示のまま(次フェーズ開始時に戻す)。
    /// </summary>
    private IEnumerator ExplodeCharacter(int p)
    {
        if (playerTransforms == null || p < 0 || p >= playerTransforms.Length) yield break;
        var t = playerTransforms[p];
        if (t == null) yield break;
        var sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null || sr.sprite.texture == null) yield break;

        var sprite = sr.sprite;
        Texture2D tex = sprite.texture;
        Rect trect = sprite.textureRect;
        float ppu = sprite.pixelsPerUnit;
        Bounds bounds = sr.bounds;
        Vector3 charScale = sr.transform.lossyScale;
        Color baseCol = sr.color;
        int sortLayer = sr.sortingLayerID;
        int sortOrder = sr.sortingOrder + 2;

        // 元キャラを隠す(破片で置き換わる)
        foreach (var s in t.GetComponentsInChildren<SpriteRenderer>(true))
            if (s != null) s.enabled = false;

        const int cols = 6, rows = 8;
        int n = cols * rows;
        var go = new GameObject[n];
        var sp = new Sprite[n];
        var fsr = new SpriteRenderer[n];
        var vel = new Vector3[n];
        var angVel = new float[n];

        Vector3 center = bounds.center;
        float cellPxW = trect.width / cols, cellPxH = trect.height / rows;
        float cellWW = bounds.size.x / cols, cellWH = bounds.size.y / rows;

        int idx = 0;
        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++, idx++)
            {
                Rect cell = new Rect(trect.x + cx * cellPxW, trect.y + cy * cellPxH, cellPxW, cellPxH);
                sp[idx] = Sprite.Create(tex, cell, new Vector2(0.5f, 0.5f), ppu);

                var g = new GameObject("CharFrag");
                go[idx] = g;
                var r = g.AddComponent<SpriteRenderer>();
                fsr[idx] = r;
                r.sprite = sp[idx];
                r.color = baseCol;
                r.sortingLayerID = sortLayer;
                r.sortingOrder = sortOrder;

                Vector3 cellCenter = new Vector3(
                    bounds.min.x + (cx + 0.5f) * cellWW,
                    bounds.min.y + (cy + 0.5f) * cellWH, 0f);
                g.transform.position = cellCenter;
                g.transform.localScale = charScale;

                Vector3 dir = cellCenter - center; dir.z = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                    dir = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f);
                dir = dir.normalized;
                float speed = UnityEngine.Random.Range(18f, 34f);   // すごいスピードで散る
                vel[idx] = dir * speed + Vector3.up * UnityEngine.Random.Range(6f, 14f);
                angVel[idx] = UnityEngine.Random.Range(-1080f, 1080f);
            }
        }

        const float dur = 0.45f;   // 短く一気に弾けて待機へ戻る
        const float grav = -24f;
        float el = 0f;
        while (el < dur)
        {
            float dt = Time.deltaTime;
            el += dt;
            float alpha = 1f - Mathf.Clamp01(el / dur); alpha *= alpha; // 後半で速くフェード
            for (int i = 0; i < n; i++)
            {
                if (go[i] == null) continue;
                vel[i].y += grav * dt;
                go[i].transform.position += vel[i] * dt;
                go[i].transform.Rotate(0f, 0f, angVel[i] * dt);
                Color c = baseCol; c.a = baseCol.a * alpha;
                fsr[i].color = c;
            }
            yield return null;
        }

        for (int i = 0; i < n; i++)
        {
            if (go[i] != null) Destroy(go[i]);
            if (sp[i] != null) Destroy(sp[i]);
        }
    }

    /// <summary>
    /// プレイヤーを「窓(上半身)の待機」に戻す: 描画を再表示＋ポートレート配置＋窓クリップ＋浮遊(待機)の基準更新。
    /// 分解(爆発)で消えたキャラも、これで元の待機モーションへ戻る。
    /// </summary>
    private void ShowPlayerWindow(int p)
    {
        if (frameUI == null || playerTransforms == null || p < 0 || p >= playerTransforms.Length) return;
        var t = playerTransforms[p];
        if (t == null) return;

        // 分解で消していた描画を戻す(待機に戻る)
        foreach (var s in t.GetComponentsInChildren<SpriteRenderer>(true))
            if (s != null) s.enabled = true;

        var sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            frameUI.GetAllyPortraitPlacement(p, sr.sprite, out Vector3 pos, out float scale);
            t.position = pos;
            t.localScale = new Vector3(scale, scale, 1f);
        }
        frameUI.SetPlayerWindowClip(p, true);

        // 浮遊(待機モーション)を新しい窓位置基準に合わせる
        var fm = t.GetComponent<UnderTheSpotlight.FloatingCharacterMotion>();
        if (fm != null) fm.RebaseToCurrentPosition();
    }

    private void EnsureBossRenderers()
    {
        if (bossSrs != null) return;
        GameObject e = GameObject.Find("Enemy");
        if (e == null)
        {
            var eoc = FindObjectOfType<EnemyOnFieldController>();
            if (eoc != null) e = eoc.gameObject;
        }
        if (e == null) return;
        bossSrs = e.GetComponentsInChildren<SpriteRenderer>(true);
        bossBaseColors = new Color[bossSrs.Length];
        for (int i = 0; i < bossSrs.Length; i++)
            bossBaseColors[i] = (bossSrs[i] != null) ? bossSrs[i].color : Color.white;
    }

    /// <summary>カード選択中: ボスを暗くする。</summary>
    private void DarkenBoss()
    {
        EnsureBossRenderers();
        if (bossSrs == null) return;
        for (int i = 0; i < bossSrs.Length; i++)
        {
            if (bossSrs[i] == null) continue;
            Color b = bossBaseColors[i];
            bossSrs[i].color = new Color(b.r * BossDimMul, b.g * BossDimMul, b.b * BossDimMul, b.a);
        }
    }

    /// <summary>部位選択/終了時: ボスを元の色に戻す。</summary>
    private void RestoreBoss()
    {
        if (bossSrs == null) return;
        for (int i = 0; i < bossSrs.Length; i++)
            if (bossSrs[i] != null) bossSrs[i].color = bossBaseColors[i];
    }

    /// <summary>シーン上の Enemy の Visual(SpriteRenderer 子)を返す。無ければ Enemy ルート。</summary>
    private Transform ResolveEnemyVisual()
    {
        GameObject e = GameObject.Find("Enemy");
        if (e == null)
        {
            var eoc = FindObjectOfType<EnemyOnFieldController>();
            if (eoc != null) e = eoc.gameObject;
        }
        if (e == null) return null;
        var v = e.transform.Find("Visual");
        return v != null ? v : e.transform;
    }

    private void FinishSelection()
    {
        UnityEngine.Debug.Log("[CardSelectPhase] 全員選択完了(カード+部位) → ActionCommand へ");
        AllSelected?.Invoke(selections);

        // 部位選択結果をミニゲームへ引き継ぐ(PartSelect フェーズは新フローでスキップ)
        if (partSelectPhase != null && partTargets != null)
            partSelectPhase.SetTargetPartIds(partTargets);

        if (stateMachine != null)
        {
            stateMachine.ChangeState(BattleStateMachine.State.ActionCommand);
        }
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ======= キャラクター位置管理 =======

    // キャラクターの元の位置を保存
    private Vector3[] originalCharacterPositions;

    /// <summary>
    /// キャラクターを元の位置に戻す
    /// </summary>
    private void RestoreCharacterPosition(int playerIndex)
    {
        if (playerTransforms == null || playerIndex >= playerTransforms.Length || playerTransforms[playerIndex] == null)
        {
            return;
        }

        // 元の位置が保存されていない場合は保存
        if (originalCharacterPositions == null)
        {
            originalCharacterPositions = new Vector3[playerTransforms.Length];
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                if (playerTransforms[i] != null)
                {
                    originalCharacterPositions[i] = playerTransforms[i].position;
                }
            }
        }

        if (originalCharacterPositions != null && playerIndex < originalCharacterPositions.Length)
        {
            playerTransforms[playerIndex].position = originalCharacterPositions[playerIndex];
        }
    }

    // ======= 選択カード表示演出 =======

    /// <summary>
    /// 選択された4枚のカードを下から表示する演出
    /// </summary>
    private IEnumerator ShowSelectedCardsFromBottom()
    {
        isAnimating = true;

        // プレイヤーフレームのみ非表示（ボスフレームは表示したまま）
        frameUI.HideAllyFramesOnly();

        // 選択されたカードを生成して下から登場
        Vector2 frameSize = frameUI.GetAllyFrameSize();
        selectedCardObjects = new GameObject[4];

        // 4つのカードスロット位置（4枠に均等配置）
        Vector3[] slotPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            slotPositions[i] = frameUI.GetAllyFrameCenter(i);
        }

        // 画面下中央の開始位置（デッキから配られる感じ）
        float screenBottom = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, -0.2f, 0f)).y;
        Vector3 deckPosition = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, -0.2f, 0f));
        deckPosition.z = 0f;

        // 各カードを順番に配る（P1から順番に）
        float staggerDelay = 0.15f;
        float animDuration = 0.4f;

        for (int i = 0; i < playerCount; i++)
        {
            if (selections[i] == null) continue;

            Vector3 targetPos = slotPositions[i];

            // カードを生成（デッキ位置から開始）
            GameObject cardObj = CreateSelectedCardVisual(selections[i], deckPosition, frameSize, i);
            selectedCardObjects[i] = cardObj;

            // 配られるアニメーション開始（デッキから各スロットへ）
            StartCoroutine(AnimateCardDeal(cardObj, deckPosition, targetPos, animDuration, i));

            yield return new WaitForSeconds(staggerDelay);
        }

        // 最後のカードのアニメーション完了を待つ
        yield return new WaitForSeconds(animDuration + 0.1f);

        // 合計レベルを計算
        int totalLevel = 0;
        for (int i = 0; i < playerCount; i++)
        {
            if (selections[i] != null)
            {
                totalLevel += selections[i].UpgradeLevel + 1;
            }
        }

        // 黒背景と文字を表示
        GameObject bannerObj = CreateLevelBanner(totalLevel);

        // 4人分の準備完了ボタンを表示（各自カーソルを重ねて決定キーで押す）
        GameObject readyUIObj = CreatePlayerReadyUI();
        bool[] playerReady = new bool[4];

        // この間は全カーソルを不透明にして全員操作しやすくする
        cursorsAllActive = true;

        // 4人全員が自分のボタンを押すまで待機
        while (!AllPlayersReady(playerReady))
        {
            // ボタンのホバー表示を更新
            UpdateReadyButtonHover(readyUIObj, playerReady);

            // 決定キーが押されたら、自分のボタンにカーソルが乗っているプレイヤーを準備完了に
            var kb = Keyboard.current;
            if (kb != null && ConfirmPressed(kb))
            {
                for (int p = 0; p < playerCount; p++)
                {
                    if (playerReady[p]) continue;
                    if (IsCursorOnReadyButton(p))
                    {
                        playerReady[p] = true;
                        FillPlayerCircle(readyUIObj, p);
                    }
                }
            }
            yield return null;
        }

        cursorsAllActive = false;

        // 全員Ready後、少し待機
        yield return new WaitForSeconds(0.5f);

        // バナーとReadyUIをフェードアウト
        yield return StartCoroutine(FadeOutBannerAndReadyUI(bannerObj, readyUIObj));

        // カードをP1から順番に下に退場させる
        yield return StartCoroutine(DismissSelectedCardsSequentially());

        // プレイヤーフレームを元に戻す
        frameUI.ShowAllyFramesOnly();

        // キャラクターを元の位置に復帰して4人待機状態に
        RestoreAllPlayersVisibility();
        RestoreAllCharacterPositions();

        isAnimating = false;
    }

    /// <summary>
    /// カードを配るアニメーション（デッキから各位置へアーチを描いて飛ぶ）
    /// </summary>
    private IEnumerator AnimateCardDeal(GameObject cardObj, Vector3 startPos, Vector3 endPos, float duration, int index)
    {
        float elapsed = 0f;

        // 初期状態：裏向き風に小さく、傾ける
        float startScale = cardObj.transform.localScale.x * 0.5f;
        float endScale = cardObj.transform.localScale.x;
        cardObj.transform.localScale = Vector3.one * startScale;

        // 配る方向に傾ける
        float startRotation = 20f;
        cardObj.transform.rotation = Quaternion.Euler(0f, 0f, startRotation);

        // 中間点（アーチを描く）
        Vector3 midPoint = (startPos + endPos) / 2f + new Vector3(0f, 1.2f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutQuart(t);

            // 2次ベジェ曲線でアーチを描く
            Vector3 p0 = startPos;
            Vector3 p1 = midPoint;
            Vector3 p2 = endPos;
            Vector3 currentPos = (1 - easeT) * (1 - easeT) * p0 + 2 * (1 - easeT) * easeT * p1 + easeT * easeT * p2;
            cardObj.transform.position = currentPos;

            // スケールを徐々に大きく
            float currentScale = Mathf.Lerp(startScale, endScale, easeT);
            cardObj.transform.localScale = Vector3.one * currentScale;

            // 回転を戻す
            float currentRotation = Mathf.Lerp(startRotation, 0f, EaseOutBack(t));
            cardObj.transform.rotation = Quaternion.Euler(0f, 0f, currentRotation);

            yield return null;
        }

        // 最終位置に確定
        cardObj.transform.position = endPos;
        cardObj.transform.localScale = Vector3.one * endScale;
        cardObj.transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// カードをP1から順番に下に退場させる
    /// </summary>
    private IEnumerator DismissSelectedCardsSequentially()
    {
        float duration = 0.3f;
        float staggerDelay = 0.1f;
        float screenBottom = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, -0.3f, 0f)).y;

        for (int i = 0; i < 4; i++)
        {
            if (selectedCardObjects[i] == null) continue;

            // 各カードを下に退場させるアニメーション開始
            StartCoroutine(AnimateCardDismissDown(selectedCardObjects[i], screenBottom, duration, i));

            yield return new WaitForSeconds(staggerDelay);
        }

        // 最後のカードのアニメーション完了を待つ
        yield return new WaitForSeconds(duration);

        // カードを破棄
        for (int i = 0; i < 4; i++)
        {
            if (selectedCardObjects[i] != null)
            {
                Destroy(selectedCardObjects[i]);
            }
        }
        selectedCardObjects = null;
    }

    /// <summary>
    /// カードを下に退場させるアニメーション
    /// </summary>
    private IEnumerator AnimateCardDismissDown(GameObject cardObj, float targetY, float duration, int index)
    {
        float elapsed = 0f;
        Vector3 startPos = cardObj.transform.position;
        Vector3 endPos = new Vector3(startPos.x, targetY, 0f);
        float startScale = cardObj.transform.localScale.x;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseInQuad(t);

            // 位置を下へ
            cardObj.transform.position = Vector3.Lerp(startPos, endPos, easeT);

            // 少し縮小しながら
            float currentScale = Mathf.Lerp(startScale, startScale * 0.7f, easeT);
            cardObj.transform.localScale = Vector3.one * currentScale;

            // 少し回転しながら退場
            float rotation = Mathf.Lerp(0f, -10f + index * 3f, easeT);
            cardObj.transform.rotation = Quaternion.Euler(0f, 0f, rotation);

            yield return null;
        }
    }

    /// <summary>
    /// 黒背景バナーと合計レベル・ミニゲーム開始文字を表示
    /// </summary>
    private GameObject CreateLevelBanner(int totalLevel)
    {
        GameObject bannerObj = new GameObject("LevelBanner");

        // 画面の左右端を取得
        Vector3 leftEdge = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0.5f, 10f));
        Vector3 rightEdge = Camera.main.ViewportToWorldPoint(new Vector3(1f, 0.5f, 10f));
        Vector3 center = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10f));
        center.z = 0f;

        float bannerWidth = rightEdge.x - leftEdge.x;
        float bannerHeight = 2.5f;  // バナーの高さ

        bannerObj.transform.position = center;

        // 黒背景を作成
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(bannerObj.transform);
        bgObj.transform.localPosition = Vector3.zero;

        var bgSr = bgObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = CreateBannerSprite();
        bgSr.color = new Color(0f, 0f, 0f, 0.75f);  // 透明度75%の黒
        bgSr.sortingOrder = 290;

        // バナーサイズを画面幅に合わせる
        float spriteWidth = bgSr.sprite.bounds.size.x;
        float spriteHeight = bgSr.sprite.bounds.size.y;
        bgObj.transform.localScale = new Vector3(bannerWidth / spriteWidth, bannerHeight / spriteHeight, 1f);

        // Total Level テキスト（上段）
        GameObject totalObj = new GameObject("TotalLevel");
        totalObj.transform.SetParent(bannerObj.transform);
        totalObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        var totalMesh = totalObj.AddComponent<TextMesh>();
        totalMesh.text = $"Total Level {totalLevel}";
        totalMesh.fontSize = 64;
        totalMesh.characterSize = 0.12f;
        totalMesh.anchor = TextAnchor.MiddleCenter;
        totalMesh.alignment = TextAlignment.Center;
        totalMesh.color = Color.white;
        totalMesh.fontStyle = FontStyle.Bold;

        var totalRenderer = totalObj.GetComponent<MeshRenderer>();
        totalRenderer.sortingOrder = 300;

        // MiniGame Start テキスト（下段）
        GameObject startObj = new GameObject("MiniGameStart");
        startObj.transform.SetParent(bannerObj.transform);
        startObj.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        var startMesh = startObj.AddComponent<TextMesh>();
        startMesh.text = "MiniGame Start";
        startMesh.fontSize = 72;
        startMesh.characterSize = 0.14f;
        startMesh.anchor = TextAnchor.MiddleCenter;
        startMesh.alignment = TextAlignment.Center;
        startMesh.color = Color.white;
        startMesh.fontStyle = FontStyle.Bold;

        var startRenderer = startObj.GetComponent<MeshRenderer>();
        startRenderer.sortingOrder = 300;

        // バナー登場アニメーション
        StartCoroutine(AnimateBannerAppear(bannerObj));

        return bannerObj;
    }

    /// <summary>
    /// バナー用のスプライトを生成
    /// </summary>
    private Sprite CreateBannerSprite()
    {
        int width = 64;
        int height = 64;
        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 64);
    }

    /// <summary>
    /// バナー登場アニメーション
    /// </summary>
    private IEnumerator AnimateBannerAppear(GameObject bannerObj)
    {
        float duration = 0.3f;
        float elapsed = 0f;

        // 初期状態：縦に潰れた状態
        Vector3 startScale = new Vector3(1f, 0f, 1f);
        Vector3 endScale = Vector3.one;

        bannerObj.transform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutBack(t);

            bannerObj.transform.localScale = Vector3.Lerp(startScale, endScale, easeT);
            yield return null;
        }

        bannerObj.transform.localScale = endScale;
    }

    /// <summary>
    /// 4人の入力待ちUIを作成（円を4つ横並び）
    /// </summary>
    private GameObject CreatePlayerReadyUI()
    {
        GameObject readyUI = new GameObject("PlayerReadyUI");

        // バナーの下に配置
        Vector3 centerPos = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.32f, 10f));
        centerPos.z = 0f;
        readyUI.transform.position = centerPos;

        float circleSpacing = 1.5f;
        // 参加人数ぶんの円を中央寄せで並べる(4人未満でも中央に揃う)
        float startX = -circleSpacing * (playerCount - 1) / 2f;

        readyButtonWorldPos = new Vector3[4];

        for (int i = 0; i < playerCount; i++)
        {
            readyButtonWorldPos[i] = centerPos + new Vector3(startX + i * circleSpacing, 0f, 0f);

            // 円の外枠（輪っか）
            GameObject circleObj = new GameObject($"Circle_P{i + 1}");
            circleObj.transform.SetParent(readyUI.transform);
            circleObj.transform.localPosition = new Vector3(startX + i * circleSpacing, 0f, 0f);

            var outlineSr = circleObj.AddComponent<SpriteRenderer>();
            outlineSr.sprite = CreateCircleSprite(64, false);  // 輪っか
            outlineSr.color = GetPlayerColor(i);
            outlineSr.sortingOrder = 305;
            circleObj.transform.localScale = Vector3.one * 0.8f;

            // 円の中身（最初は透明）
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(circleObj.transform);
            fillObj.transform.localPosition = Vector3.zero;

            var fillSr = fillObj.AddComponent<SpriteRenderer>();
            fillSr.sprite = CreateCircleSprite(64, true);  // 塗りつぶし
            fillSr.color = new Color(GetPlayerColor(i).r, GetPlayerColor(i).g, GetPlayerColor(i).b, 0f);  // 透明
            fillSr.sortingOrder = 304;
            fillObj.transform.localScale = Vector3.one * 0.85f;

            // プレイヤー番号テキスト
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(circleObj.transform);
            labelObj.transform.localPosition = new Vector3(0f, -0.7f, 0f);

            var labelMesh = labelObj.AddComponent<TextMesh>();
            labelMesh.text = $"P{i + 1}";
            labelMesh.fontSize = 32;
            labelMesh.characterSize = 0.08f;
            labelMesh.anchor = TextAnchor.MiddleCenter;
            labelMesh.alignment = TextAlignment.Center;
            labelMesh.color = GetPlayerColor(i);
            labelMesh.fontStyle = FontStyle.Bold;

            var labelRenderer = labelObj.GetComponent<MeshRenderer>();
            labelRenderer.sortingOrder = 306;
        }

        return readyUI;
    }

    /// <summary>
    /// 円のスプライトを生成
    /// </summary>
    private Sprite CreateCircleSprite(int size, bool filled)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        float center = size / 2f;
        float radius = size / 2f - 2f;
        float innerRadius = filled ? 0f : radius - 4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius && dist >= innerRadius)
                {
                    // アンチエイリアス
                    float alpha = 1f;
                    if (dist > radius - 1f) alpha = radius - dist;
                    if (!filled && dist < innerRadius + 1f) alpha = Mathf.Min(alpha, dist - innerRadius);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// 全プレイヤーがReadyかチェック
    /// </summary>
    private bool AllPlayersReady(bool[] playerReady)
    {
        for (int i = 0; i < playerCount; i++)
        {
            if (!playerReady[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// プレイヤー p のカーソルが自分の準備完了ボタンに乗っているか。
    /// </summary>
    private bool IsCursorOnReadyButton(int p)
    {
        if (readyButtonWorldPos == null || cursorViewportPos == null) return false;
        if (p < 0 || p >= 4 || p >= cursorViewportPos.Length) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        float depth = Mathf.Abs(cam.transform.position.z);
        Vector3 c = cam.ViewportToWorldPoint(new Vector3(cursorViewportPos[p].x, cursorViewportPos[p].y, depth));
        c.z = 0f;

        Vector2 cur = new Vector2(c.x, c.y);
        Vector2 btn = new Vector2(readyButtonWorldPos[p].x, readyButtonWorldPos[p].y);
        return Vector2.Distance(cur, btn) < readyButtonRadius;
    }

    /// <summary>
    /// 準備完了ボタンのホバー表示を更新（未Readyのボタンをカーソルが乗ると少し拡大）。
    /// </summary>
    private void UpdateReadyButtonHover(GameObject readyUI, bool[] playerReady)
    {
        if (readyUI == null) return;
        float k = 1f - Mathf.Exp(-12f * Time.deltaTime);

        for (int p = 0; p < playerCount; p++)
        {
            if (playerReady[p]) continue;   // Ready済みは FillPlayerCircle 側の演出に任せる
            Transform circle = readyUI.transform.Find($"Circle_P{p + 1}");
            if (circle == null) continue;

            bool hover = IsCursorOnReadyButton(p);
            float target = hover ? 0.95f : 0.8f;
            circle.localScale = Vector3.Lerp(circle.localScale, Vector3.one * target, k);
        }
    }

    /// <summary>
    /// プレイヤーの円を埋める
    /// </summary>
    private void FillPlayerCircle(GameObject readyUI, int playerIndex)
    {
        Transform circleTransform = readyUI.transform.Find($"Circle_P{playerIndex + 1}");
        if (circleTransform == null) return;

        Transform fillTransform = circleTransform.Find("Fill");
        if (fillTransform == null) return;

        var fillSr = fillTransform.GetComponent<SpriteRenderer>();
        if (fillSr != null)
        {
            Color playerColor = GetPlayerColor(playerIndex);
            fillSr.color = new Color(playerColor.r, playerColor.g, playerColor.b, 1f);
        }

        // 円を埋めるアニメーション
        StartCoroutine(AnimateCircleFill(circleTransform.gameObject));
    }

    /// <summary>
    /// 円を埋めるアニメーション
    /// </summary>
    private IEnumerator AnimateCircleFill(GameObject circleObj)
    {
        float duration = 0.15f;
        float elapsed = 0f;

        Vector3 originalScale = circleObj.transform.localScale;
        Vector3 punchScale = originalScale * 1.3f;

        // パンチスケール
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(1.3f, 1f, EaseOutBack(t));
            circleObj.transform.localScale = originalScale * scale;
            yield return null;
        }

        circleObj.transform.localScale = originalScale;
    }

    /// <summary>
    /// バナーとReadyUIをフェードアウト
    /// </summary>
    private IEnumerator FadeOutBannerAndReadyUI(GameObject bannerObj, GameObject readyUIObj)
    {
        float duration = 0.4f;
        float elapsed = 0f;

        // 全てのSpriteRendererとMeshRendererを取得
        var bannerSprites = bannerObj.GetComponentsInChildren<SpriteRenderer>();
        var bannerMeshes = bannerObj.GetComponentsInChildren<MeshRenderer>();
        var readySprites = readyUIObj.GetComponentsInChildren<SpriteRenderer>();
        var readyMeshes = readyUIObj.GetComponentsInChildren<MeshRenderer>();

        // 元の色を保存
        Color[] bannerSpriteColors = new Color[bannerSprites.Length];
        for (int i = 0; i < bannerSprites.Length; i++)
            bannerSpriteColors[i] = bannerSprites[i].color;

        Color[] readySpriteColors = new Color[readySprites.Length];
        for (int i = 0; i < readySprites.Length; i++)
            readySpriteColors[i] = readySprites[i].color;

        // TextMeshの色を取得
        var bannerTexts = bannerObj.GetComponentsInChildren<TextMesh>();
        var readyTexts = readyUIObj.GetComponentsInChildren<TextMesh>();

        Color[] bannerTextColors = new Color[bannerTexts.Length];
        for (int i = 0; i < bannerTexts.Length; i++)
            bannerTextColors[i] = bannerTexts[i].color;

        Color[] readyTextColors = new Color[readyTexts.Length];
        for (int i = 0; i < readyTexts.Length; i++)
            readyTextColors[i] = readyTexts[i].color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;

            // スプライトのフェード
            for (int i = 0; i < bannerSprites.Length; i++)
            {
                Color c = bannerSpriteColors[i];
                bannerSprites[i].color = new Color(c.r, c.g, c.b, c.a * alpha);
            }
            for (int i = 0; i < readySprites.Length; i++)
            {
                Color c = readySpriteColors[i];
                readySprites[i].color = new Color(c.r, c.g, c.b, c.a * alpha);
            }

            // テキストのフェード
            for (int i = 0; i < bannerTexts.Length; i++)
            {
                Color c = bannerTextColors[i];
                bannerTexts[i].color = new Color(c.r, c.g, c.b, c.a * alpha);
            }
            for (int i = 0; i < readyTexts.Length; i++)
            {
                Color c = readyTextColors[i];
                readyTexts[i].color = new Color(c.r, c.g, c.b, c.a * alpha);
            }

            yield return null;
        }

        // 削除
        Destroy(bannerObj);
        Destroy(readyUIObj);
    }

    /// <summary>
    /// 選択されたカードのビジュアルを生成（レベル表示付き）
    /// </summary>
    private GameObject CreateSelectedCardVisual(CardInstance card, Vector3 position, Vector2 frameSize, int playerIndex)
    {
        GameObject cardObj = new GameObject($"SelectedCard_P{playerIndex + 1}_{card.Data.displayName}");
        cardObj.transform.position = position;

        // 選択後の待機時カードサイズ（四隅に並ぶ）。小さすぎたので拡大。
        float scale = Mathf.Min(frameSize.x, frameSize.y) * 0.22f;
        cardObj.transform.localScale = Vector3.one * scale;

        // 洗練されたTCGスタイルのカードSpriteを取得
        Sprite cardSprite = CardVisualGenerator.GetCardSprite(card);

        // カード本体（スプライト画像）
        var bgSr = cardObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = cardSprite;
        bgSr.sortingOrder = 200;

        // プレイヤー番号表示（右下に配置）- カード名と同じフォント設定
        GameObject playerObj = new GameObject("PlayerNumber");
        playerObj.transform.SetParent(cardObj.transform);
        playerObj.transform.localPosition = new Vector3(0.9f, -1.8f, 0f);

        var playerMesh = playerObj.AddComponent<TextMesh>();
        playerMesh.text = $"P{playerIndex + 1}";
        playerMesh.fontSize = 42;
        playerMesh.characterSize = 0.082f;
        playerMesh.anchor = TextAnchor.MiddleCenter;
        playerMesh.alignment = TextAlignment.Center;
        playerMesh.color = GetPlayerColor(playerIndex);
        playerMesh.fontStyle = FontStyle.Bold;

        var playerRenderer = playerObj.GetComponent<MeshRenderer>();
        playerRenderer.sortingOrder = 205;
        ApplyCardFont(playerMesh);

        return cardObj;
    }

    /// <summary>
    /// 全キャラクターを元の位置に復帰
    /// </summary>
    private void RestoreAllCharacterPositions()
    {
        if (playerTransforms == null || originalCharacterPositions == null) return;

        for (int i = 0; i < playerTransforms.Length; i++)
        {
            if (playerTransforms[i] != null && i < originalCharacterPositions.Length)
            {
                playerTransforms[i].position = originalCharacterPositions[i];
            }
        }
    }

    /// <summary>
    /// レベルに応じた色を取得
    /// </summary>
    // カードの文字に規定フォント(GameFont=PixelMplus/misaki)を適用。正典: Docs/Specifications/デザインテーマ.md
    private static Font _cardFont;
    private void ApplyCardFont(TextMesh tm)
    {
        if (tm == null) return;
        if (_cardFont == null) _cardFont = GameFont.Body();
        if (_cardFont == null) return;
        tm.font = _cardFont;
        var mr = tm.GetComponent<MeshRenderer>();
        if (mr != null && _cardFont.material != null) mr.material = _cardFont.material;
    }

    // カードレベルを丸数字(①②③…)で表現。色はレベルに応じて変化。規定フォントで描画。
    private void CreateLevelBadge(GameObject cardObj, int level, int sortingOrder = 104)
    {
        string txt = CircledNumber(Mathf.Max(1, level));
        // 明るめの色(レベル色を白寄りに)
        Color col = Color.Lerp(GetLevelColor(Mathf.Clamp(level, 1, 5)), Color.white, 0.4f);
        // 少し右下寄りに配置
        Vector3 pos = new Vector3(-1.0f, 1.62f, 0f);

        // 影(暗色・右下にずらして後ろ)
        MakeLevelText(cardObj, "CardLevelShadow", txt, new Color(0f, 0f, 0f, 0.7f),
            pos + new Vector3(0.08f, -0.08f, 0f), sortingOrder - 1);
        // 本体(明るい色)
        MakeLevelText(cardObj, "CardLevel", txt, col, pos, sortingOrder);
    }

    private void MakeLevelText(GameObject cardObj, string name, string txt, Color color, Vector3 localPos, int sortingOrder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(cardObj.transform);
        obj.transform.localPosition = localPos;

        var mesh = obj.AddComponent<TextMesh>();
        mesh.text = txt;
        mesh.fontSize = 48;
        mesh.characterSize = 0.12f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        mesh.fontStyle = FontStyle.Bold;

        var mr = obj.GetComponent<MeshRenderer>();
        mr.sortingOrder = sortingOrder;
        ApplyCardFont(mesh);
    }

    // 1→① 2→② … 20→⑳。範囲外は素の数字。
    private static string CircledNumber(int n)
    {
        if (n >= 1 && n <= 20) return ((char)(0x2460 + (n - 1))).ToString();
        return n.ToString();
    }

    private Color GetLevelColor(int level)
    {
        switch (level)
        {
            case 1: return new Color(0.6f, 0.6f, 0.6f);      // グレー
            case 2: return new Color(0.2f, 0.7f, 0.3f);      // 緑
            case 3: return new Color(0.3f, 0.5f, 0.9f);      // 青
            case 4: return new Color(0.7f, 0.3f, 0.8f);      // 紫
            case 5: return new Color(1.0f, 0.8f, 0.2f);      // 金
            default: return Color.white;
        }
    }

    /// <summary>
    /// プレイヤー番号に応じた色を取得
    /// </summary>
    private Color GetPlayerColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return new Color(0.9f, 0.3f, 0.3f);      // 赤
            case 1: return new Color(0.3f, 0.6f, 0.9f);      // 青
            case 2: return new Color(0.3f, 0.8f, 0.4f);      // 緑
            case 3: return new Color(0.9f, 0.7f, 0.2f);      // 黄
            default: return Color.white;
        }
    }
}
