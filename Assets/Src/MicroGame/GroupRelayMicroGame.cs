using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// グループ型(リレー式)アクションコマンドの共通基底。
/// 1本の大きなバーを参加人数ぶんの「領域」に分割し、P1→P2→… の順に手番プレイヤーが自分の領域で操作する。
/// 手番で成功(Hit)したら <see cref="MicroGameContext.OnPlayerAttack"/> を発火し、
/// 光が次の領域へ「シュッ」と移って次のプレイヤーの手番になる。
///
/// 派生クラスは「領域の中身(BuildZone)」と「手番中の判定(TickTurn)」だけを実装すればよい。
/// 仕様: docs/Design/ActionCommand_Design.md「グループ型アクションコマンド一覧」
/// </summary>
public abstract class GroupRelayMicroGame : IMicroGame
{
    public abstract string DisplayName { get; }
    public bool Succeeded { get; private set; }

    /// <summary>手番1回分の判定結果。</summary>
    protected enum TurnResult { Continue, Hit, Miss }

    // ---- バー寸法(スクリーン画素, 1920x1080 基準) ----
    protected const float BarW = 940f;
    protected const float BarH = 92f;
    protected const float Pad = 8f;

    protected MicroGameContext Ctx { get; private set; }
    protected int PlayerCount { get; private set; }
    /// <summary>手番プレイヤー(0始まり)。</summary>
    protected int Current { get; private set; }
    /// <summary>手番開始からの経過秒。</summary>
    protected float PlayerTime { get; private set; }
    /// <summary>1人あたりの制限時間(秒)。<see cref="ComputeTurnLimit"/> で決まる。</summary>
    protected float PerPlayerLimit { get; private set; }
    protected float InnerW { get; private set; }
    protected float ZoneW { get; private set; }
    /// <summary>領域(下地)の内寸。BuildZone に渡す RectTransform の大きさ。</summary>
    protected float ZoneInnerW => ZoneW - 3f;
    protected float ZoneInnerH => BarH - Pad * 2f;

    /// <summary>手番終了時に領域へ「HIT!/MISS」を表示するか。</summary>
    protected virtual bool ShowResultLabels => true;
    /// <summary>画面上部に出す操作説明。</summary>
    protected abstract string PromptText { get; }

    private Color[] colors;
    private GameObject root;
    private Image[] zoneBg;
    private Text[] resultLabels;
    private RectTransform highlightRt;
    private RectTransform lightRt;
    private Image lightImg;

    private bool switching;
    private float switchT;
    private const float SwitchDur = 0.26f;
    private int switchFrom, switchTo;
    private bool finished;

    // ================= ライフサイクル =================

    public void Begin(MicroGameContext c)
    {
        if (c == null)
        {
            Debug.LogError($"[{GetType().Name}] MicroGameContext が null です。ActionCommandPhase から渡されるコンテキストを確認してください。");
            finished = true;
            return;
        }
        if (c.uiRoot == null)
        {
            Debug.LogError($"[{GetType().Name}] MicroGameContext.uiRoot が null です。UI を生成できません。");
            finished = true;
            return;
        }

        Ctx = c;
        PlayerCount = Mathf.Max(1, c.playerCount);
        colors = c.playerColors;
        InnerW = BarW - Pad * 2f;
        ZoneW = InnerW / PlayerCount;
        PerPlayerLimit = ComputeTurnLimit(c);
        Current = 0;
        PlayerTime = 0f;
        switching = false;
        finished = false;
        Succeeded = false;

        OnSetup(c);

        root = new GameObject(GetType().Name, typeof(RectTransform));
        root.transform.SetParent(c.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        var guiHelper = root.AddComponent<MicroGameIMGUIHelper>();
        guiHelper.Initialize(() => PromptText);
        guiHelper.SetTimer(() => PlayerTime, () => PerPlayerLimit);

        // 外枠 → 下地
        var outer = AcUi.Image(root.transform, "BarOuter", new Color(0f, 0f, 0f, 1f));
        AcUi.Place(outer.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BarW + 12f, BarH + 12f));
        var bg = AcUi.Image(outer.transform, "BarBg", new Color(0.10f, 0.11f, 0.16f, 1f));
        AcUi.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BarW, BarH));

        zoneBg = new Image[PlayerCount];
        resultLabels = new Text[PlayerCount];

        for (int i = 0; i < PlayerCount; i++)
        {
            Color col = PlayerColor(i);
            float leftX = -InnerW / 2f + i * ZoneW;

            // 領域の下地(暗いプレイヤーカラー)
            var zbg = AcUi.Image(bg.transform, $"ZoneBg{i}", Dim(col, 0.22f));
            PlaceLeft(zbg.rectTransform, leftX, ZoneInnerW, ZoneInnerH);
            zoneBg[i] = zbg;

            // 派生クラス固有の中身
            BuildZone(i, zbg.rectTransform, col);

            // プレイヤーラベル P1..P4
            var lbl = AcUi.Label(bg.transform, $"P{i + 1}", 20, col, TextAnchor.MiddleCenter, true, true);
            PlaceCenter(lbl.rectTransform, leftX + ZoneW / 2f, -(BarH / 2f + 22f), ZoneW, 30f);

            // 成否ラベル(バーの上。手番終了まで非表示)
            var res = AcUi.Label(bg.transform, $"Result{i}", 26, Color.white, TextAnchor.MiddleCenter, true, true);
            PlaceCenter(res.rectTransform, leftX + ZoneW / 2f, BarH / 2f + 26f, ZoneW, 34f);
            res.text = "";
            resultLabels[i] = res;
        }

        // 領域の区切り線
        for (int i = 1; i < PlayerCount; i++)
        {
            float x = -InnerW / 2f + i * ZoneW;
            var div = AcUi.Image(bg.transform, $"Div{i}", new Color(0f, 0f, 0f, 0.7f));
            PlaceCenter(div.rectTransform, x, 0f, 3f, BarH - Pad);
        }

        // 手番領域のハイライト
        var hl = AcUi.Image(bg.transform, "Highlight", new Color(1f, 1f, 1f, 0f));
        highlightRt = hl.rectTransform;
        MoveHighlightTo(Current);

        // 交代の光(初期は非表示)
        lightImg = AcUi.Image(bg.transform, "SwitchLight", new Color(1f, 1f, 1f, 0f));
        lightRt = lightImg.rectTransform;
        lightRt.anchorMin = lightRt.anchorMax = new Vector2(0.5f, 0.5f);
        lightRt.pivot = new Vector2(0.5f, 0.5f);
        lightRt.sizeDelta = new Vector2(46f, 46f);

        OnTurnStart(Current);
    }

    public bool Tick(float dt)
    {
        if (finished) return true;

        if (switching)
        {
            TickSwitch(dt);
            return false;
        }

        PlayerTime += dt;
        TurnResult r = TickTurn(Current, dt);

        // 手番領域を少し脈動させる
        if (highlightRt != null)
        {
            float pulse = 0.35f + 0.25f * (Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f);
            SetImageAlpha(highlightRt, pulse);
        }

        if (r == TurnResult.Continue && PlayerTime >= PerPlayerLimit) r = TurnResult.Miss;
        if (r != TurnResult.Continue) EndTurn(r == TurnResult.Hit);
        return false;
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

    /// <summary>領域 i の中身を生成する。zone は pivot 左・内寸 ZoneInnerW x ZoneInnerH。</summary>
    protected abstract void BuildZone(int i, RectTransform zone, Color col);

    /// <summary>プレイヤー i の手番が始まった(交代アニメ完了直後)。</summary>
    protected virtual void OnTurnStart(int i) { }

    /// <summary>手番中の毎フレーム判定。時間切れは基底が Miss として扱う。</summary>
    protected abstract TurnResult TickTurn(int i, float dt);

    /// <summary>プレイヤー i の手番が終わった(成否確定直後)。</summary>
    protected virtual void OnTurnEnd(int i, bool hit) { }

    // ================= 内部処理 =================

    private void EndTurn(bool hit)
    {
        int i = Current;
        if (hit)
        {
            SetAttackSource(i);              // ダメージ光の始点=この領域の末端
            Ctx.OnPlayerAttack?.Invoke(i);   // このプレイヤーの攻撃命中
        }
        if (ShowResultLabels && resultLabels != null && resultLabels[i] != null)
        {
            resultLabels[i].text = hit ? "HIT!" : "MISS";
            resultLabels[i].color = hit ? new Color(1f, 0.92f, 0.35f, 1f) : new Color(0.65f, 0.65f, 0.72f, 1f);
        }
        OnTurnEnd(i, hit);
        BeginSwitch(i, i + 1);
    }

    private void BeginSwitch(int from, int to)
    {
        // 次がいなければ終了
        if (to >= PlayerCount)
        {
            Succeeded = true;
            finished = true;
            return;
        }
        switching = true;
        switchT = 0f;
        switchFrom = from;
        switchTo = to;

        Color col = PlayerColor(to);
        if (lightImg != null) lightImg.color = new Color(col.r, col.g, col.b, 1f);
        if (lightRt != null)
        {
            lightRt.anchoredPosition = new Vector2(ZoneCenterX(from), 0f);
            lightRt.sizeDelta = new Vector2(60f, 60f);
        }
        if (highlightRt != null) SetImageAlpha(highlightRt, 0f);
    }

    private void TickSwitch(float dt)
    {
        switchT += dt;
        float p = Mathf.Clamp01(switchT / SwitchDur);
        float ease = 1f - (1f - p) * (1f - p); // easeOut

        if (lightRt != null)
        {
            float x = Mathf.Lerp(ZoneCenterX(switchFrom), ZoneCenterX(switchTo), ease);
            lightRt.anchoredPosition = new Vector2(x, 0f);
            float s = Mathf.Lerp(62f, 26f, p);
            lightRt.sizeDelta = new Vector2(s, s);
            SetImageAlpha(lightRt, 1f - 0.3f * p);
        }

        if (p >= 1f)
        {
            switching = false;
            Current = switchTo;
            PlayerTime = 0f;
            if (lightRt != null) SetImageAlpha(lightRt, 0f);
            MoveHighlightTo(Current);
            OnTurnStart(Current);
        }
    }

    private void MoveHighlightTo(int i)
    {
        if (highlightRt == null) return;
        float leftX = -InnerW / 2f + i * ZoneW;
        PlaceLeft(highlightRt, leftX - 2f, ZoneW + 1f, BarH - Pad);
        Color col = PlayerColor(i);
        var img = highlightRt.GetComponent<Image>();
        if (img != null) img.color = new Color(col.r, col.g, col.b, 0.4f);
    }

    private float ZoneCenterX(int i) => -InnerW / 2f + (i + 0.5f) * ZoneW;

    // 領域 i の「末端(右端)中央」のスクリーン座標を ctx に記録(ダメージ光の始点用)。
    private void SetAttackSource(int i)
    {
        if (Ctx == null || zoneBg == null || i < 0 || i >= zoneBg.Length || zoneBg[i] == null) return;
        var corners = new Vector3[4];
        zoneBg[i].rectTransform.GetWorldCorners(corners); // 0=BL,1=TL,2=TR,3=BR (Overlay=画素)
        Vector3 rightMid = (corners[2] + corners[3]) * 0.5f;
        Ctx.LastAttackScreenPos = new Vector2(rightMid.x, rightMid.y);
        Ctx.HasLastAttackScreenPos = true;
    }

    // ================= 派生クラス向けヘルパー =================

    protected Color PlayerColor(int i)
    {
        if (colors != null && i >= 0 && i < colors.Length) return colors[i];
        return Color.white;
    }

    protected static Color Dim(Color c, float a) => new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, a);

    protected static void SetImageAlpha(RectTransform rt, float a)
    {
        if (rt == null) return;
        var img = rt.GetComponent<Image>();
        if (img != null) { var c = img.color; img.color = new Color(c.r, c.g, c.b, a); }
    }

    /// <summary>親中心基準で「左端 x・幅・高さ」で配置(pivot 左)。</summary>
    protected static void PlaceLeft(RectTransform rt, float leftX, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(leftX, 0f);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>親中心基準で「中心 x,y・幅・高さ」で配置(pivot 中央)。</summary>
    protected static void PlaceCenter(RectTransform rt, float cx, float cy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>領域内の「左端からの x・中心からの y」に中心を置く(pivot 中央)。BuildZone の子に使う。</summary>
    protected static void PlaceInZone(RectTransform rt, float xFromLeft, float yFromCenter, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(xFromLeft, yFromCenter);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>領域内の左端から伸びる塗り(pivot 左)。幅は呼び出し側が sizeDelta.x で更新する。</summary>
    protected static RectTransform CreateLeftFill(Transform zone, string name, Color col, float h)
    {
        var f = AcUi.Image(zone, name, col);
        var rt = f.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, h);
        return rt;
    }
}
