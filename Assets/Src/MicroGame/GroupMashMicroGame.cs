using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 【仮】アクションコマンド: 1本の大きなゲージを 4プレイヤー分の領域に分割。
/// 各領域はプレイヤーカラーで塗られ、手番のプレイヤーが SPACE 連打で自分の領域を満たす。
/// 満たしたら OnPlayerAttack を発火し、弾幕の交代のように「光でシュッ」と次の領域へ移る。
/// グループ型(全員分を1回で処理)として ActionCommandPhase から実行される。
/// </summary>
public class GroupMashMicroGame : IMicroGame
{
    public string DisplayName => "れんだ！";
    public bool Succeeded { get; private set; }

    private MicroGameContext ctx;
    private GameObject root;
    private MicroGameIMGUIHelper guiHelper;

    private int playerCount;
    private Color[] colors;

    // ゲージ寸法(スクリーン画素)
    private const float BarW = 940f;
    private const float BarH = 92f;
    private const float Pad = 8f;
    private float innerW;
    private float zoneW;

    private RectTransform[] fillRt;     // 各領域の塗り
    private Image[] zoneBg;             // 各領域の下地
    private RectTransform highlightRt;  // 手番領域の枠
    private RectTransform lightRt;       // 交代の光
    private Image lightImg;

    private float[] fill;   // 各プレイヤーの充填 0..1
    private int current;    // 手番プレイヤー
    private float playerTime;
    private float perPlayerLimit;
    private float gainPerPress;
    private float decayPerSec;

    // 交代アニメ
    private bool switching;
    private float switchT;
    private const float SwitchDur = 0.26f;
    private int switchFrom, switchTo;

    private bool finished;

    public void Begin(MicroGameContext c)
    {
        ctx = c;
        playerCount = Mathf.Max(1, c.playerCount);
        colors = c.playerColors;
        innerW = BarW - Pad * 2f;
        zoneW = innerW / playerCount;

        // 難易度: かなり易しめ(仮)。1押しで大きく増え、減衰は小さく、時間も長め。
        perPlayerLimit = Mathf.Max(4f, c.timeLimit);
        gainPerPress = Mathf.Max(0.12f, 0.17f / Mathf.Max(0.7f, c.speedMul));
        decayPerSec = 0.08f + 0.05f * c.speedMul;

        fill = new float[playerCount];
        current = 0;
        playerTime = 0f;
        switching = false;
        finished = false;

        root = new GameObject("GroupMash", typeof(RectTransform));
        root.transform.SetParent(c.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        guiHelper = root.AddComponent<MicroGameIMGUIHelper>();
        guiHelper.Initialize(() => "SPACE!");
        guiHelper.SetTimer(() => playerTime, () => perPlayerLimit);

        // 外枠 → 下地
        var outer = AcUi.Image(root.transform, "BarOuter", new Color(0f, 0f, 0f, 1f));
        AcUi.Place(outer.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BarW + 12f, BarH + 12f));
        var bg = AcUi.Image(outer.transform, "BarBg", new Color(0.10f, 0.11f, 0.16f, 1f));
        AcUi.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BarW, BarH));

        fillRt = new RectTransform[playerCount];
        zoneBg = new Image[playerCount];

        for (int i = 0; i < playerCount; i++)
        {
            Color col = PlayerColor(i);
            float leftX = -innerW / 2f + i * zoneW;

            // 領域の下地(暗いプレイヤーカラー)
            var zbg = AcUi.Image(bg.transform, $"ZoneBg{i}", Dim(col, 0.22f));
            PlaceLeft(zbg.rectTransform, leftX, zoneW - 3f, BarH - Pad * 2f);
            zoneBg[i] = zbg;

            // 領域の塗り(明るいプレイヤーカラー・左から伸びる)
            var f = AcUi.Image(zbg.transform, $"Fill{i}", col);
            var frt = f.rectTransform;
            frt.anchorMin = new Vector2(0f, 0.5f);
            frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(0f, 0f);
            frt.sizeDelta = new Vector2(0f, BarH - Pad * 2f - 4f);
            fillRt[i] = frt;

            // プレイヤーラベル P1..P4
            var lbl = AcUi.Label(bg.transform, $"P{i + 1}", 20, col, TextAnchor.MiddleCenter, true, true);
            PlaceLeftCenter(lbl.rectTransform, leftX + zoneW / 2f, -(BarH / 2f + 22f), zoneW, 30f);
        }

        // 領域の区切り線
        for (int i = 1; i < playerCount; i++)
        {
            float x = -innerW / 2f + i * zoneW;
            var div = AcUi.Image(bg.transform, $"Div{i}", new Color(0f, 0f, 0f, 0.7f));
            PlaceLeftCenter(div.rectTransform, x, 0f, 3f, BarH - Pad);
        }

        // 手番領域のハイライト(手番色でうっすら・脈動)
        var hl = AcUi.Image(bg.transform, "Highlight", new Color(1f, 1f, 1f, 0f));
        highlightRt = hl.rectTransform;
        MoveHighlightTo(current);

        // 交代の光(初期は非表示)
        var lg = AcUi.Image(bg.transform, "SwitchLight", new Color(1f, 1f, 1f, 0f));
        lightImg = lg;
        lightRt = lg.rectTransform;
        lightRt.anchorMin = lightRt.anchorMax = new Vector2(0.5f, 0.5f);
        lightRt.pivot = new Vector2(0.5f, 0.5f);
        lightRt.sizeDelta = new Vector2(46f, 46f);
    }

    public bool Tick(float dt)
    {
        if (finished) return true;

        if (switching)
        {
            TickSwitch(dt);
            return false;
        }

        // 手番プレイヤーの連打
        playerTime += dt;
        fill[current] -= decayPerSec * dt;
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) fill[current] += gainPerPress;
        fill[current] = Mathf.Clamp01(fill[current]);

        UpdateFillVisual(current);

        // 手番領域を少し脈動させる
        if (highlightRt != null)
        {
            float pulse = 0.35f + 0.25f * (Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f);
            SetImageAlpha(highlightRt, pulse);
        }

        bool done = fill[current] >= 1f;
        bool timeout = playerTime >= perPlayerLimit;

        if (done || timeout)
        {
            if (done)
            {
                fill[current] = 1f;
                UpdateFillVisual(current);
                SetAttackSource(current);              // ダメージ光の始点=この領域の末端
                ctx.OnPlayerAttack?.Invoke(current);   // このプレイヤーの攻撃命中
            }
            BeginSwitch(current, current + 1);
        }
        return false;
    }

    // ---- 交代(光でシュッ) ----
    private void BeginSwitch(int from, int to)
    {
        // 次がいなければ終了
        if (to >= playerCount)
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
            current = switchTo;
            playerTime = 0f;
            if (lightImg != null) SetImageAlpha(lightRt, 0f);
            MoveHighlightTo(current);
        }
    }

    // ---- 見た目ヘルパー ----
    private void UpdateFillVisual(int i)
    {
        if (fillRt == null || i < 0 || i >= fillRt.Length || fillRt[i] == null) return;
        float w = (zoneW - 6f) * fill[i];
        fillRt[i].sizeDelta = new Vector2(w, fillRt[i].sizeDelta.y);
    }

    private void MoveHighlightTo(int i)
    {
        if (highlightRt == null) return;
        float leftX = -innerW / 2f + i * zoneW;
        PlaceLeft(highlightRt, leftX - 2f, zoneW + 1f, BarH - Pad);
        // 手番色でうっすら
        Color col = PlayerColor(i);
        var img = highlightRt.GetComponent<Image>();
        if (img != null) img.color = new Color(col.r, col.g, col.b, 0.4f);
    }

    private float ZoneCenterX(int i) => -innerW / 2f + (i + 0.5f) * zoneW;

    // 領域 i の「末端(右端)中央」のスクリーン座標を ctx に記録(ダメージ光の始点用)。
    private void SetAttackSource(int i)
    {
        if (ctx == null || zoneBg == null || i < 0 || i >= zoneBg.Length || zoneBg[i] == null) return;
        var corners = new Vector3[4];
        zoneBg[i].rectTransform.GetWorldCorners(corners); // 0=BL,1=TL,2=TR,3=BR (Overlay=画素)
        Vector3 rightMid = (corners[2] + corners[3]) * 0.5f;
        ctx.LastAttackScreenPos = new Vector2(rightMid.x, rightMid.y);
        ctx.HasLastAttackScreenPos = true;
    }

    private Color PlayerColor(int i)
    {
        if (colors != null && i >= 0 && i < colors.Length) return colors[i];
        return Color.white;
    }

    private static Color Dim(Color c, float a) => new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, a);

    private static void SetImageAlpha(RectTransform rt, float a)
    {
        if (rt == null) return;
        var img = rt.GetComponent<Image>();
        if (img != null) { var c = img.color; img.color = new Color(c.r, c.g, c.b, a); }
    }

    // bar 中心基準で「左端 x・幅・高さ」で配置(pivot 左)
    private static void PlaceLeft(RectTransform rt, float leftX, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(leftX, 0f);
        rt.sizeDelta = new Vector2(w, h);
    }

    // bar 中心基準で「中心 x,y・幅・高さ」で配置(pivot 中央)
    private static void PlaceLeftCenter(RectTransform rt, float cx, float cy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
