using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// グループ型(リレー式)アクションコマンドの共通基底 ＝「アクションコマンド・フィールド」。
///
/// 画面中央やや下に統一デザインのフィールドを1枚出し、P1→P2→… の順に1人ずつ、
/// そのフィールド全体を使ってミニゲームを遊ぶ(4人でも1つ1つが小さくならないように、同時分割はしない)。
///
///   ┌ ヘッダー: [P1] ゲーム名 / 操作説明 ─────────┐
///   │ プレイエリア(各ミニゲームが丸・三角などで描く)│
///   └ フッター: 残り時間バー ・ 手番チップ P1..P4 ─┘
///
/// 1手番の流れ: 準備(「P1 の ばん！」) → プレイ → 結果(HIT!/MISS) → 次のプレイヤー。
/// HIT した瞬間に <see cref="MicroGameContext.OnPlayerAttack"/> を発火し、攻撃演出とダメージが入る。
///
/// 派生クラスは BuildPlay(プレイエリアの中身を1回作る) / OnTurnStart(手番ごとの初期化) /
/// TickTurn(判定) を実装するだけでよい。
/// 仕様: docs/Design/ActionCommand_Design.md「グループ型アクションコマンド一覧」
/// </summary>
public abstract class GroupRelayMicroGame : IMicroGame
{
    public abstract string DisplayName { get; }
    public bool Succeeded { get; private set; }

    /// <summary>手番1回分の判定結果。</summary>
    protected enum TurnResult { Continue, Hit, Miss }

    // ---- フィールド寸法(1920x1080 基準のキャンバス座標) ----
    private const float FieldW = 700f;
    private const float FieldH = 440f;
    private static readonly Vector2 FieldCenter = new Vector2(0f, -140f); // ボスと攻撃表示(上側)を隠さない位置
    private const float BorderW = 6f;
    private const float HeaderH = 84f;
    private const float FooterH = 60f;

    /// <summary>プレイエリアの大きさ。BuildPlay に渡す RectTransform(中心原点)の内寸。</summary>
    protected const float PlayW = FieldW - BorderW * 2f - 24f;
    protected const float PlayH = FieldH - BorderW * 2f - HeaderH - FooterH;

    // ---- 手番の間の演出時間 ----
    private const float ReadyDur = 0.6f;
    private const float ResultDur = 0.65f;

    protected MicroGameContext Ctx { get; private set; }
    protected int PlayerCount { get; private set; }
    /// <summary>手番プレイヤー(0始まり)。</summary>
    protected int Current { get; private set; }
    /// <summary>プレイ開始(準備の後)からの経過秒。</summary>
    protected float PlayerTime { get; private set; }
    /// <summary>1人あたりの制限時間(秒)。<see cref="ComputeTurnLimit"/> で決まる。</summary>
    protected float PerPlayerLimit { get; private set; }

    /// <summary>ヘッダーに出す操作説明。</summary>
    protected abstract string PromptText { get; }

    private enum Stage { Ready, Play, Result, Done }
    private Stage stage;
    private float stageT;

    private Color[] colors;
    private int[] turnState; // 0=未 1=HIT 2=MISS

    // UI 参照
    private GameObject root;
    private Image frameImg;
    private Image headerChip;
    private Text headerChipText;
    private RectTransform playArea;
    private Image timerFill;
    private Image[] orderChips;
    private Image[] orderRings;
    private Text[] orderTexts;
    private Text centerMsg;

    // ================= ライフサイクル =================

    public void Begin(MicroGameContext c)
    {
        if (c == null)
        {
            Debug.LogError($"[{GetType().Name}] MicroGameContext が null です。ActionCommandPhase から渡されるコンテキストを確認してください。");
            stage = Stage.Done;
            return;
        }
        if (c.uiRoot == null)
        {
            Debug.LogError($"[{GetType().Name}] MicroGameContext.uiRoot が null です。アクションコマンドの UI を生成できません。");
            stage = Stage.Done;
            return;
        }

        Ctx = c;
        PlayerCount = Mathf.Max(1, c.playerCount);
        colors = c.playerColors;
        PerPlayerLimit = ComputeTurnLimit(c);
        turnState = new int[PlayerCount];
        Succeeded = false;

        OnSetup(c);
        BuildField(c.uiRoot);
        EnterReady(0);
    }

    public bool Tick(float dt)
    {
        switch (stage)
        {
            case Stage.Ready:
                stageT += dt;
                if (stageT >= ReadyDur)
                {
                    stage = Stage.Play;
                    PlayerTime = 0f;
                    SetCenterMsg("", Color.white);
                }
                return false;

            case Stage.Play:
            {
                PlayerTime += dt;
                TurnResult r = TickTurn(Current, dt);
                if (r == TurnResult.Continue && PlayerTime >= PerPlayerLimit) r = TurnResult.Miss;
                if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(1f - PlayerTime / PerPlayerLimit);
                if (r != TurnResult.Continue) EndTurn(r == TurnResult.Hit);
                return false;
            }

            case Stage.Result:
                stageT += dt;
                if (stageT >= ResultDur)
                {
                    int next = Current + 1;
                    if (next >= PlayerCount)
                    {
                        Succeeded = true;
                        stage = Stage.Done;
                        return true;
                    }
                    EnterReady(next);
                }
                return false;

            default:
                return true;
        }
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }

    // ================= 派生クラスのフック =================

    /// <summary>1人あたりの制限時間。既定は ctx.timeLimit(下限 3 秒)。</summary>
    protected virtual float ComputeTurnLimit(MicroGameContext c) => Mathf.Max(3f, c.timeLimit);

    /// <summary>UI 生成前に呼ばれる。難易度パラメータや配列の初期化に使う。</summary>
    protected virtual void OnSetup(MicroGameContext c) { }

    /// <summary>プレイエリアの中身を1回だけ生成する。play は中心原点・内寸 PlayW x PlayH。</summary>
    protected abstract void BuildPlay(RectTransform play);

    /// <summary>プレイヤー i の手番の準備(「P? の ばん！」表示と同時)。見た目をこのプレイヤー用に戻す。</summary>
    protected abstract void OnTurnStart(int i, Color col);

    /// <summary>プレイ中の毎フレーム判定。時間切れは基底が Miss として扱う。</summary>
    protected abstract TurnResult TickTurn(int i, float dt);

    /// <summary>プレイヤー i の手番が終わった(成否確定直後)。結果の見た目を出す。</summary>
    protected virtual void OnTurnEnd(int i, bool hit) { }

    // ================= 手番進行 =================

    private void EnterReady(int i)
    {
        Current = i;
        stage = Stage.Ready;
        stageT = 0f;
        PlayerTime = 0f;

        Color col = PlayerColor(i);
        if (frameImg != null) frameImg.color = col;
        if (headerChip != null) headerChip.color = col;
        if (headerChipText != null) headerChipText.text = $"P{i + 1}";
        if (timerFill != null) { timerFill.fillAmount = 1f; timerFill.color = col; }
        RefreshOrderChips();

        OnTurnStart(i, col);
        SetCenterMsg($"P{i + 1} の ばん！", col);
    }

    private void EndTurn(bool hit)
    {
        int i = Current;
        turnState[i] = hit ? 1 : 2;
        if (hit)
        {
            SetAttackSource();               // ダメージ光の始点=プレイエリア中央
            Ctx.OnPlayerAttack?.Invoke(i);   // このプレイヤーの攻撃命中
        }
        OnTurnEnd(i, hit);
        SetCenterMsg(hit ? "HIT!" : "MISS", hit ? HitColor : MissColor);
        RefreshOrderChips();
        stage = Stage.Result;
        stageT = 0f;
    }

    // ================= フィールド UI =================

    protected static readonly Color HitColor = new Color(1f, 0.92f, 0.35f, 1f);
    protected static readonly Color MissColor = new Color(0.62f, 0.62f, 0.70f, 1f);
    protected static readonly Color FieldBg = new Color(0.07f, 0.08f, 0.13f, 0.96f);

    private void BuildField(RectTransform parent)
    {
        root = new GameObject(GetType().Name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // 外枠(手番プレイヤー色) → 下地
        frameImg = AcUi.Image(root.transform, "FieldFrame", Color.white);
        AcUi.Place(frameImg.rectTransform, new Vector2(0.5f, 0.5f), FieldCenter, new Vector2(FieldW, FieldH));
        var bg = AcUi.Image(frameImg.transform, "FieldBg", FieldBg);
        PlaceCenter(bg.rectTransform, Vector2.zero, new Vector2(FieldW - BorderW * 2f, FieldH - BorderW * 2f));
        float innerW = FieldW - BorderW * 2f;
        float innerH = FieldH - BorderW * 2f;

        // ---- ヘッダー ----
        float headerY = innerH / 2f - HeaderH / 2f;
        var header = AcUi.Image(bg.transform, "Header", new Color(1f, 1f, 1f, 0.06f));
        PlaceCenter(header.rectTransform, new Vector2(0f, headerY), new Vector2(innerW, HeaderH));

        headerChip = Shape(header.transform, "PlayerChip", AcShapes.Circle(), Color.white,
            new Vector2(-innerW / 2f + 50f, 0f), new Vector2(64f, 64f));
        headerChipText = AcUi.Label(headerChip.transform, "PlayerChipText", 26, new Color(0.08f, 0.08f, 0.12f, 1f), TextAnchor.MiddleCenter, true, false);
        PlaceCenter(headerChipText.rectTransform, Vector2.zero, new Vector2(64f, 64f));

        var title = AcUi.Label(header.transform, "Title", 34, Color.white, TextAnchor.MiddleLeft, true, true);
        title.text = DisplayName;
        PlaceLeftAt(title.rectTransform, -innerW / 2f + 96f, 16f, innerW - 120f, 40f);
        var prompt = AcUi.Label(header.transform, "Prompt", 22, new Color(0.85f, 0.87f, 0.95f, 1f), TextAnchor.MiddleLeft, false, true);
        prompt.text = PromptText;
        PlaceLeftAt(prompt.rectTransform, -innerW / 2f + 98f, -20f, innerW - 120f, 28f);

        // ---- プレイエリア ----
        float playY = innerH / 2f - HeaderH - PlayH / 2f;
        var playGo = new GameObject("PlayArea", typeof(RectTransform));
        playGo.transform.SetParent(bg.transform, false);
        playArea = playGo.GetComponent<RectTransform>();
        PlaceCenter(playArea, new Vector2(0f, playY), new Vector2(PlayW, PlayH));
        BuildPlay(playArea);

        // 中央メッセージ(「P1 の ばん！」/ HIT! / MISS)。プレイ要素より手前に描くため最後に作る
        centerMsg = AcUi.Label(playArea, "CenterMsg", 56, Color.white, TextAnchor.MiddleCenter, true, true);
        PlaceCenter(centerMsg.rectTransform, Vector2.zero, new Vector2(PlayW, 90f));
        centerMsg.text = "";

        // ---- フッター ----
        float footerY = -innerH / 2f + FooterH / 2f;
        var footer = AcUi.Image(bg.transform, "Footer", new Color(1f, 1f, 1f, 0.06f));
        PlaceCenter(footer.rectTransform, new Vector2(0f, footerY), new Vector2(innerW, FooterH));

        // 手番チップ(右側): P1..Pn。順番と結果が一目で分かる
        const float chipD = 40f;
        const float chipGap = 14f;
        float chipsW = PlayerCount * chipD + (PlayerCount - 1) * chipGap;
        float chipsLeft = innerW / 2f - 20f - chipsW;
        orderChips = new Image[PlayerCount];
        orderRings = new Image[PlayerCount];
        orderTexts = new Text[PlayerCount];
        for (int i = 0; i < PlayerCount; i++)
        {
            var pos = new Vector2(chipsLeft + chipD / 2f + i * (chipD + chipGap), 0f);
            orderRings[i] = Shape(footer.transform, $"OrderRing{i}", AcShapes.Ring(), Color.white, pos, new Vector2(chipD + 12f, chipD + 12f));
            orderChips[i] = Shape(footer.transform, $"OrderChip{i}", AcShapes.Circle(), Color.white, pos, new Vector2(chipD, chipD));
            orderTexts[i] = AcUi.Label(orderChips[i].transform, $"OrderText{i}", 17, Color.white, TextAnchor.MiddleCenter, true, false);
            PlaceCenter(orderTexts[i].rectTransform, Vector2.zero, new Vector2(chipD, chipD));
            orderTexts[i].text = $"P{i + 1}";
        }

        // 残り時間バー(左側): 左から縮む
        float timerLeft = -innerW / 2f + 24f;
        float timerW = Mathf.Max(120f, chipsLeft - 30f - timerLeft);
        var timerBg = AcUi.Image(footer.transform, "TimerBg", new Color(0f, 0f, 0f, 0.55f));
        PlaceLeftAt(timerBg.rectTransform, timerLeft, 0f, timerW, 18f);
        timerFill = AcUi.Image(timerBg.transform, "TimerFill", Color.white);
        AcUi.Stretch(timerFill.rectTransform, 3f);
        timerFill.type = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        timerFill.sprite = AcShapes.Square(); // Filled 表示には sprite が必要
        timerFill.fillAmount = 1f;
    }

    private void RefreshOrderChips()
    {
        if (orderChips == null) return;
        for (int i = 0; i < PlayerCount; i++)
        {
            Color col = PlayerColor(i);
            bool isCurrent = (i == Current) && turnState[i] == 0;
            Color chip;
            Color text = new Color(0.08f, 0.08f, 0.12f, 1f);
            switch (turnState[i])
            {
                case 1: chip = HitColor; break;                            // HIT
                case 2: chip = new Color(0.30f, 0.30f, 0.36f, 1f); text = MissColor; break; // MISS
                default: chip = isCurrent ? col : Dim(col, 0.55f); break;  // 手番中 / 未
            }
            orderChips[i].color = chip;
            orderTexts[i].color = text;
            orderRings[i].color = isCurrent ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
    }

    private void SetCenterMsg(string msg, Color col)
    {
        if (centerMsg == null) return;
        centerMsg.text = msg;
        centerMsg.color = col;
    }

    // プレイエリア中央のスクリーン座標を ctx に記録(ダメージ光の始点用)。
    private void SetAttackSource()
    {
        if (Ctx == null || playArea == null) return;
        var corners = new Vector3[4];
        playArea.GetWorldCorners(corners); // Overlay キャンバスでは画素座標
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        Ctx.LastAttackScreenPos = new Vector2(center.x, center.y);
        Ctx.HasLastAttackScreenPos = true;
    }

    // ================= 派生クラス向けヘルパー =================

    protected Color PlayerColor(int i)
    {
        if (colors != null && i >= 0 && i < colors.Length) return colors[i];
        return Color.white;
    }

    protected static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    protected static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>図形(丸/リング/三角)を親の中心基準で置く。</summary>
    protected static Image Shape(Transform parent, string name, Sprite sprite, Color col, Vector2 pos, Vector2 size)
    {
        var img = AcUi.Image(parent, name, col);
        img.sprite = sprite;
        PlaceCenter(img.rectTransform, pos, size);
        return img;
    }

    /// <summary>親の中心基準で中心 pos・大きさ size に置く(pivot 中央)。</summary>
    protected static void PlaceCenter(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>親の中心基準で「左端 x・中心 y」に置く(pivot 左)。</summary>
    protected static void PlaceLeftAt(RectTransform rt, float leftX, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(leftX, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
