using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [Tooltip("フレーム集合アニメーション時間")]
    [SerializeField] private float collapseAnimDuration = 0.4f;
    [Tooltip("フレーム展開アニメーション時間")]
    [SerializeField] private float expandAnimDuration = 0.5f;
    [Tooltip("カード登場アニメーション時間")]
    [SerializeField] private float cardEnterDuration = 0.3f;
    [Tooltip("カード登場間隔")]
    [SerializeField] private float cardEnterStagger = 0.1f;

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

        int n = Mathf.Max(1, playerCount);
        selections = new CardInstance[n];
        turnOrder = BuildTurnOrder(n);
        turnIndex = 0;

        UnityEngine.Debug.Log($"[CardSelectPhase] ターン順序: {string.Join(",", turnOrder)}");

        // フェーズコルーチンを開始
        phaseCoroutine = StartCoroutine(CardSelectSequence());
    }

    public void Exit()
    {
        enabled = false;
        turnActive = false;
        isAnimating = false;

        if (phaseCoroutine != null)
        {
            StopCoroutine(phaseCoroutine);
            phaseCoroutine = null;
        }

        DestroyAllCards();

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
        // カード選択フェーズ開始時に元の位置を保存
        SaveOriginalCharacterPositions();

        // 最初に全フレームを左に集合（キャラクターも一緒に）
        yield return StartCoroutine(frameUI.CollapseAllFramesToLeft(collapseAnimDuration, playerTransforms));

        // 各プレイヤーのターンを処理
        for (turnIndex = 0; turnIndex < turnOrder.Length; turnIndex++)
        {
            activePlayer = turnOrder[turnIndex];

            // 対象キャラクターを画面外左に配置してから表示
            Transform charTransform = GetPlayerTransform(activePlayer);
            if (charTransform != null)
            {
                // まず画面外左に配置（CollapseAllFramesToLeftの目標位置と同じ）
                charTransform.position = frameUI.GetCollapseTargetPosition();
            }
            // 位置を設定してから1フレーム待ってから表示
            yield return null;
            ShowOnlyActivePlayer(activePlayer);

            // フレームを展開（キャラクターも一緒に移動）
            yield return StartCoroutine(frameUI.ExpandForCardSelect(activePlayer, expandAnimDuration, charTransform));

            // スポットライト演出を開始
            Vector3 spotlightPos = frameUI.GetAllyFrameCenter(0); // 左端フレーム
            frameUI.ShowSpotlight(spotlightPos, 1.2f);

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

            // カードを右3つのスロットに配置
            yield return StartCoroutine(SpawnCardsAnimation(currentHand));

            // 選択待機
            selectedCardIndex = -1;
            cursorIndex = 0;
            UpdateCardHighlights();
            turnActive = true;
            timeLeft = selectTimeLimit;

            UnityEngine.Debug.Log($"[CardSelectPhase] P{activePlayer + 1} の手番開始 / 手札{currentHand.Count}枚");

            // 選択が完了するまで待機
            while (selectedCardIndex < 0)
            {
                // タイムアウト処理
                if (selectTimeLimit > 0f)
                {
                    timeLeft -= Time.deltaTime;
                    if (timeLeft <= 0f)
                    {
                        selectedCardIndex = cursorIndex;
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

            // カード退場アニメーション（選択カードは下へ、他は左へ）
            // キャラクターとフレームも一緒に左へ退場
            Transform charTransformExit = GetPlayerTransform(activePlayer);
            StartCoroutine(frameUI.CollapseFrameWithCharacter(activePlayer, 0.35f, charTransformExit));
            yield return StartCoroutine(AnimateCardsExit(selectedCardIndex));

            // カードを消す
            DestroyAllCards();

            // スポットライト演出を終了
            frameUI.HideSpotlight();

            // キャラクターを非表示に（位置は戻さない、画面外にいるので）
            HidePlayer(activePlayer);

            // 次のプレイヤーがいる場合は再び左に集合
            if (turnIndex < turnOrder.Length - 1)
            {
                yield return StartCoroutine(frameUI.CollapseAllFramesToLeft(collapseAnimDuration, null));
            }
        }

        // 全員完了後、選ばれたカード4枚を下から表示する演出
        yield return StartCoroutine(ShowSelectedCardsFromBottom());

        // 次のフェーズへ
        FinishSelection();
    }

    public void Tick()
    {
        if (!turnActive || isAnimating) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // 左右でカーソル移動
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
        {
            MoveCursor(-1);
        }
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
        {
            MoveCursor(+1);
        }
        // 確定
        else if (kb.enterKey.wasPressedThisFrame
              || kb.numpadEnterKey.wasPressedThisFrame
              || kb.spaceKey.wasPressedThisFrame
              || kb.zKey.wasPressedThisFrame)
        {
            ConfirmSelection();
        }
    }

    private void MoveCursor(int delta)
    {
        if (currentHand == null || currentHand.Count == 0) return;

        cursorIndex += delta;
        if (cursorIndex < 0) cursorIndex = currentHand.Count - 1;
        if (cursorIndex >= currentHand.Count) cursorIndex = 0;

        UpdateCardHighlights();
    }

    private void ConfirmSelection()
    {
        selectedCardIndex = cursorIndex;
    }

    private void UpdateCardHighlights()
    {
        if (cardObjects == null) return;

        Vector2 frameSize = frameUI.GetAllyFrameSize();
        float baseScale = Mathf.Min(frameSize.x, frameSize.y) * 0.17f;

        for (int i = 0; i < cardObjects.Length; i++)
        {
            if (cardObjects[i] == null) continue;

            // 選択中のカードは明るく大きく、それ以外は少し暗くして小さく
            if (i == cursorIndex)
            {
                cardObjects[i].transform.localScale = Vector3.one * baseScale * 1.15f;
                SetCardBrightness(cardObjects[i], i, 1f);

                // グローを強調
                var glow = cardObjects[i].transform.Find("Glow");
                if (glow != null)
                {
                    var glowSr = glow.GetComponent<SpriteRenderer>();
                    if (glowSr != null)
                    {
                        Color c = glowSr.color;
                        glowSr.color = new Color(c.r, c.g, c.b, 0.7f);
                    }
                }
            }
            else
            {
                cardObjects[i].transform.localScale = Vector3.one * baseScale * 0.95f;
                SetCardBrightness(cardObjects[i], i, 0.55f);

                // グローを弱める
                var glow = cardObjects[i].transform.Find("Glow");
                if (glow != null)
                {
                    var glowSr = glow.GetComponent<SpriteRenderer>();
                    if (glowSr != null)
                    {
                        Color c = glowSr.color;
                        glowSr.color = new Color(c.r, c.g, c.b, 0.15f);
                    }
                }
            }
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
    /// カードをアニメーション付きで生成（左からシャッフル演出で登場）
    /// </summary>
    private IEnumerator SpawnCardsAnimation(List<CardInstance> hand)
    {
        isAnimating = true;

        Vector3[] cardSlots = frameUI.GetCardSlotPositions();
        Vector2 frameSize = frameUI.GetAllyFrameSize();
        cardObjects = new GameObject[hand.Count];
        cardOriginalColors = new Color[hand.Count][];

        // 左端の開始位置（キャラクターの左側、画面外）
        Vector3 leftFrameCenter = frameUI.GetAllyFrameCenter(0);
        Vector3 deckPosition = leftFrameCenter + new Vector3(-3f, 0f, 0f);

        for (int i = 0; i < hand.Count && i < 3; i++)
        {
            var card = hand[i];
            Vector3 targetPos = cardSlots[i];

            // 全てのカードは左のデッキ位置から開始
            GameObject cardObj = CreateCardVisual(card, deckPosition, frameSize);
            cardObjects[i] = cardObj;

            // オリジナル色を保存
            StoreCardOriginalColors(cardObj, i);

            // シャッフル演出付きアニメーション開始
            StartCoroutine(AnimateCardShuffleEnter(cardObj, deckPosition, targetPos, cardEnterDuration + 0.15f, i));

            yield return new WaitForSeconds(cardEnterStagger);
        }

        // 最後のカードのアニメーション完了を待つ
        yield return new WaitForSeconds(cardEnterDuration + 0.2f);

        isAnimating = false;
    }

    /// <summary>
    /// シャッフル演出付きでカードを登場させる
    /// </summary>
    private IEnumerator AnimateCardShuffleEnter(GameObject cardObj, Vector3 startPos, Vector3 endPos, float duration, int cardIndex)
    {
        float elapsed = 0f;

        // 初期回転（カードが傾いた状態で始まる）
        float startRotation = -15f + cardIndex * 5f;
        cardObj.transform.rotation = Quaternion.Euler(0f, 0f, startRotation);

        // 中間点（少し上に浮かせてアーチを描く）
        Vector3 midPoint = (startPos + endPos) / 2f + new Vector3(0f, 0.8f, 0f);

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
    }

    /// <summary>
    /// カード退場アニメーション（選択カードは下へ、他は左へ）
    /// </summary>
    private IEnumerator AnimateCardsExit(int selectedIndex)
    {
        if (cardObjects == null) yield break;

        Vector3 leftFrameCenter = frameUI.GetAllyFrameCenter(0);
        Vector3 leftExitPos = leftFrameCenter + new Vector3(-4f, 0f, 0f);  // 左側へ
        Vector3 downExitPos = new Vector3(0f, -6f, 0f);  // 下へスクリーンアウト

        float exitDuration = 0.35f;

        // 全てのカードを同時に退場させる
        for (int i = 0; i < cardObjects.Length; i++)
        {
            if (cardObjects[i] == null) continue;

            Vector3 startPos = cardObjects[i].transform.position;
            Vector3 targetPos;

            if (i == selectedIndex)
            {
                // 選択されたカードは下へ
                targetPos = startPos + downExitPos;
                StartCoroutine(AnimateCardExitDown(cardObjects[i], startPos, targetPos, exitDuration));
            }
            else
            {
                // 他のカードは左へ戻る
                targetPos = leftExitPos;
                StartCoroutine(AnimateCardExitLeft(cardObjects[i], startPos, targetPos, exitDuration, i));
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

    private GameObject CreateCardVisual(CardInstance card, Vector3 position, Vector2 frameSize)
    {
        GameObject cardObj = new GameObject($"Card_{card.Data.displayName}");
        cardObj.transform.position = position;

        // カードサイズをフレームに合わせる（カード画像は367x556px）
        float scale = Mathf.Min(frameSize.x, frameSize.y) * 0.17f;
        cardObj.transform.localScale = Vector3.one * scale;

        // カード背景画像をResourcesから読み込み
        string spritePath = GetCardSpritePath(card.Data.category, card.Data.tier);
        Sprite cardSprite = Resources.Load<Sprite>(spritePath);

        // カード本体（スプライト画像）
        var bgSr = cardObj.AddComponent<SpriteRenderer>();
        if (cardSprite != null)
        {
            bgSr.sprite = cardSprite;
        }
        else
        {
            // フォールバック：スプライトがない場合は単色の角丸四角形
            bgSr.sprite = CreateFallbackCardSprite();
            bgSr.color = GetCardColor(card.Data.category);
        }
        bgSr.sortingOrder = 100;

        // グロー効果（選択時に使用）
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(cardObj.transform);
        glowObj.transform.localPosition = Vector3.zero;
        var glowSr = glowObj.AddComponent<SpriteRenderer>();
        if (cardSprite != null)
        {
            glowSr.sprite = cardSprite;
        }
        else
        {
            glowSr.sprite = CreateFallbackCardSprite();
        }
        Color glowColor = GetCardColor(card.Data.category);
        glowSr.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.3f);
        glowSr.sortingOrder = 98;
        glowObj.transform.localScale = Vector3.one * 1.1f;

        // カードのアスペクト比: 367x556 ≒ 0.66
        // 下部約25%がテキストエリア（中央から上はイラストゾーン）
        // PPU=100で、カード高さ556px → 5.56ユニット、中心から下端まで2.78ユニット
        // テキストエリアは下部25%付近

        // カード名テキスト（テキストエリア上部）
        GameObject nameObj = new GameObject("CardName");
        nameObj.transform.SetParent(cardObj.transform);
        nameObj.transform.localPosition = new Vector3(0f, -1.1f, 0f);

        var nameMesh = nameObj.AddComponent<TextMesh>();
        nameMesh.text = card.Data.displayName;
        nameMesh.fontSize = 42;
        nameMesh.characterSize = 0.065f;
        nameMesh.anchor = TextAnchor.MiddleCenter;
        nameMesh.alignment = TextAlignment.Center;
        nameMesh.color = new Color(0.15f, 0.1f, 0.05f, 1f);  // 暗い茶色
        nameMesh.fontStyle = FontStyle.Bold;

        var nameRenderer = nameObj.GetComponent<MeshRenderer>();
        nameRenderer.sortingOrder = 104;

        // 効果説明テキスト（テキストエリア下部）
        GameObject descObj = new GameObject("CardDescription");
        descObj.transform.SetParent(cardObj.transform);
        descObj.transform.localPosition = new Vector3(0f, -1.75f, 0f);

        var descMesh = descObj.AddComponent<TextMesh>();
        // 説明文が長い場合は改行（1行あたり12文字まで）
        string desc = card.Data.description ?? "";
        desc = WrapText(desc, 12);
        descMesh.text = desc;
        descMesh.fontSize = 28;
        descMesh.characterSize = 0.05f;
        descMesh.anchor = TextAnchor.MiddleCenter;
        descMesh.alignment = TextAlignment.Center;
        descMesh.color = new Color(0.25f, 0.2f, 0.15f, 1f);  // 少し明るい茶色
        descMesh.fontStyle = FontStyle.Normal;

        var descRenderer = descObj.GetComponent<MeshRenderer>();
        descRenderer.sortingOrder = 104;

        // 威力表示（テキストエリア右下）
        GameObject powerObj = new GameObject("Power");
        powerObj.transform.SetParent(cardObj.transform);
        powerObj.transform.localPosition = new Vector3(1.2f, -2.5f, 0f);

        var powerMesh = powerObj.AddComponent<TextMesh>();
        powerMesh.text = $"{card.EffectivePower}";
        powerMesh.fontSize = 36;
        powerMesh.characterSize = 0.06f;
        powerMesh.anchor = TextAnchor.MiddleCenter;
        powerMesh.alignment = TextAlignment.Center;
        powerMesh.color = new Color(0.3f, 0.2f, 0.1f, 1f);
        powerMesh.fontStyle = FontStyle.Bold;

        var powerRenderer = powerObj.GetComponent<MeshRenderer>();
        powerRenderer.sortingOrder = 104;

        // カードレベル表示（左上に配置）
        GameObject levelObj = new GameObject("CardLevel");
        levelObj.transform.SetParent(cardObj.transform);
        levelObj.transform.localPosition = new Vector3(-1.2f, 2.0f, 0f);

        var levelMesh = levelObj.AddComponent<TextMesh>();
        levelMesh.text = $"Lv.{card.UpgradeLevel + 1}";
        levelMesh.fontSize = 42;
        levelMesh.characterSize = 0.065f;
        levelMesh.anchor = TextAnchor.MiddleCenter;
        levelMesh.alignment = TextAlignment.Center;
        levelMesh.color = GetLevelColor(card.UpgradeLevel + 1);
        levelMesh.fontStyle = FontStyle.Bold;

        var levelRenderer = levelObj.GetComponent<MeshRenderer>();
        levelRenderer.sortingOrder = 104;

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

    private void FinishSelection()
    {
        UnityEngine.Debug.Log("[CardSelectPhase] 全員選択完了 → PartSelect へ");
        AllSelected?.Invoke(selections);
        if (stateMachine != null)
        {
            stateMachine.ChangeState(BattleStateMachine.State.PartSelect);
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

        for (int i = 0; i < 4; i++)
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
        for (int i = 0; i < 4; i++)
        {
            if (selections[i] != null)
            {
                totalLevel += selections[i].UpgradeLevel + 1;
            }
        }

        // 黒背景と文字を表示
        GameObject bannerObj = CreateLevelBanner(totalLevel);

        // 4人の入力待ちUIを表示
        GameObject readyUIObj = CreatePlayerReadyUI();
        bool[] playerReady = new bool[4];

        // 4人全員がEnterを押すまで待機
        while (!AllPlayersReady(playerReady))
        {
            // 各プレイヤーの入力をチェック（今はキーボードで代用）
            var kb = Keyboard.current;
            if (kb != null)
            {
                // P1: Enter, P2: Space, P3: Shift, P4: Ctrl（仮）
                if (kb.enterKey.wasPressedThisFrame && !playerReady[0])
                {
                    playerReady[0] = true;
                    FillPlayerCircle(readyUIObj, 0);
                }
                if (kb.spaceKey.wasPressedThisFrame && !playerReady[1])
                {
                    playerReady[1] = true;
                    FillPlayerCircle(readyUIObj, 1);
                }
                if (kb.leftShiftKey.wasPressedThisFrame && !playerReady[2])
                {
                    playerReady[2] = true;
                    FillPlayerCircle(readyUIObj, 2);
                }
                if (kb.leftCtrlKey.wasPressedThisFrame && !playerReady[3])
                {
                    playerReady[3] = true;
                    FillPlayerCircle(readyUIObj, 3);
                }
            }
            yield return null;
        }

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
        Vector3 centerPos = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.35f, 10f));
        centerPos.z = 0f;
        readyUI.transform.position = centerPos;

        float circleSpacing = 1.2f;
        float startX = -circleSpacing * 1.5f;

        for (int i = 0; i < 4; i++)
        {
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
        for (int i = 0; i < 4; i++)
        {
            if (!playerReady[i]) return false;
        }
        return true;
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

        // カードサイズをカード選択時と同じにする（0.17f）
        float scale = Mathf.Min(frameSize.x, frameSize.y) * 0.17f;
        cardObj.transform.localScale = Vector3.one * scale;

        // カード背景画像をResourcesから読み込み
        string spritePath = GetCardSpritePath(card.Data.category, card.Data.tier);
        Sprite cardSprite = Resources.Load<Sprite>(spritePath);

        // カード本体（スプライト画像）
        var bgSr = cardObj.AddComponent<SpriteRenderer>();
        if (cardSprite != null)
        {
            bgSr.sprite = cardSprite;
        }
        else
        {
            bgSr.sprite = CreateFallbackCardSprite();
            bgSr.color = GetCardColor(card.Data.category);
        }
        bgSr.sortingOrder = 200;

        // カード名テキスト
        GameObject nameObj = new GameObject("CardName");
        nameObj.transform.SetParent(cardObj.transform);
        nameObj.transform.localPosition = new Vector3(0f, -1.1f, 0f);

        var nameMesh = nameObj.AddComponent<TextMesh>();
        nameMesh.text = card.Data.displayName;
        nameMesh.fontSize = 42;
        nameMesh.characterSize = 0.065f;
        nameMesh.anchor = TextAnchor.MiddleCenter;
        nameMesh.alignment = TextAlignment.Center;
        nameMesh.color = new Color(0.15f, 0.1f, 0.05f, 1f);
        nameMesh.fontStyle = FontStyle.Bold;

        var nameRenderer = nameObj.GetComponent<MeshRenderer>();
        nameRenderer.sortingOrder = 204;

        // カードレベル表示（左上に配置）- カード名と同じフォント設定
        GameObject levelObj = new GameObject("CardLevel");
        levelObj.transform.SetParent(cardObj.transform);
        levelObj.transform.localPosition = new Vector3(-1.2f, 2.0f, 0f);

        var levelMesh = levelObj.AddComponent<TextMesh>();
        levelMesh.text = $"Lv.{card.UpgradeLevel + 1}";
        levelMesh.fontSize = 42;
        levelMesh.characterSize = 0.065f;
        levelMesh.anchor = TextAnchor.MiddleCenter;
        levelMesh.alignment = TextAlignment.Center;
        levelMesh.color = GetLevelColor(card.UpgradeLevel + 1);
        levelMesh.fontStyle = FontStyle.Bold;

        var levelRenderer = levelObj.GetComponent<MeshRenderer>();
        levelRenderer.sortingOrder = 205;

        // プレイヤー番号表示（右下に配置）- カード名と同じフォント設定
        GameObject playerObj = new GameObject("PlayerNumber");
        playerObj.transform.SetParent(cardObj.transform);
        playerObj.transform.localPosition = new Vector3(1.0f, -2.2f, 0f);

        var playerMesh = playerObj.AddComponent<TextMesh>();
        playerMesh.text = $"P{playerIndex + 1}";
        playerMesh.fontSize = 42;
        playerMesh.characterSize = 0.065f;
        playerMesh.anchor = TextAnchor.MiddleCenter;
        playerMesh.alignment = TextAlignment.Center;
        playerMesh.color = GetPlayerColor(playerIndex);
        playerMesh.fontStyle = FontStyle.Bold;

        var playerRenderer = playerObj.GetComponent<MeshRenderer>();
        playerRenderer.sortingOrder = 205;

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
