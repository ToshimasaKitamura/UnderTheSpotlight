using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手札 UI ビュー(レトロ・アーケード調)。
/// ランタイムで Canvas + カードスロットを生成し、カーソル移動と確定を管理する。
/// 文字は Legacy Text + OS 動的フォント(和文を確実に表示。TMP 不要)。
/// </summary>
public class CardHandView : MonoBehaviour
{
    [Tooltip("カード1枚のサイズ(px)。")]
    public Vector2 cardSize = new Vector2(220f, 310f);
    [Tooltip("横方向のカード間隔(px, カード端の隙間)。")]
    public float cardSpacing = 26f;
    [Tooltip("カード列の右端から画面右端までの余白(px)。")]
    public float rightMargin = 90f;
    [Tooltip("画面下からのカード中心オフセット(px)。")]
    public float bottomOffset = 210f;
    [Tooltip("選択中カードの拡大倍率。")]
    public float highlightScale = 1.16f;
    [Tooltip("選択中カードの上への迫り出し量(px)。")]
    public float highlightLift = 46f;
    [Tooltip("1枚あたりの配りアニメ秒数。")]
    public float slideInDuration = 0.34f;
    [Tooltip("カード間の配り遅延(秒)。1枚ずつ配られて見える。")]
    public float slideInStagger = 0.12f;
    [Tooltip("山札位置(画面右下隅からのオフセット px, x=左/y=上)。ここからカードが飛ぶ。")]
    public Vector2 deckCorner = new Vector2(140f, 120f);
    [Tooltip("確定時のポップ秒数。")]
    public float confirmPopDuration = 0.25f;
    [Tooltip("タイマーバーの幅(px)。")]
    public float timerBarWidth = 720f;

    [Header("レトロ配色")]
    [Tooltip("カード地のパネル色(暗)。")]
    public Color panelColor = new Color(0.045f, 0.05f, 0.08f, 1f);
    [Tooltip("外周アウトライン色。")]
    public Color outerColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("スキャンラインの濃さ(0=無 1=濃)。")]
    [Range(0f, 1f)] public float scanlineStrength = 0.16f;

    /// <summary>カーソルが移動したときに発火(新しい index)。</summary>
    public event Action<int> CursorMoved;
    /// <summary>カードが確定された瞬間に発火(確定 index)。</summary>
    public event Action<int> CardConfirmed;

    public int CursorIndex { get; private set; } = 0;
    public bool IsLocked { get; private set; } = false;
    public int CardCount => slots.Count;

    private Canvas canvas;
    private readonly List<CardSlot> slots = new List<CardSlot>();
    private Text headerLabel;
    private RectTransform timerFillRect;

    // フォントは GameFont 経由(GameTuning.bodyFont で上書き可能)
    private static Font UiFont() => GameFont.Body();

    public void Build(IList<CardInstance> cards)
    {
        Clear();
        if (cards == null || cards.Count == 0) return;

        BuildCanvas();
        for (int i = 0; i < cards.Count; i++)
        {
            var slot = BuildCardSlot(cards[i], i, cards.Count);
            slots.Add(slot);
        }
        CursorIndex = 0;
        ApplyHighlight();
        StartCoroutine(AnimateSlideIn());
    }

    private void BuildCanvas()
    {
        var canvasObj = new GameObject("CardSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildHeaderAndTimer();
        BuildDeckVisual();
    }

    // ---- UI ファクトリ ----
    private static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static Text MakeText(Transform parent, string name, int size,
        Color color, TextAnchor align, bool wrap, bool outline)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = UiFont();
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = wrap ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 1f);
            ol.effectDistance = new Vector2(2.5f, -2.5f);
        }
        return t;
    }

    private void BuildHeaderAndTimer()
    {
        var headerRoot = new GameObject("Header", typeof(RectTransform));
        headerRoot.transform.SetParent(canvas.transform, false);
        var hr = headerRoot.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0.5f, 1f);
        hr.anchorMax = new Vector2(0.5f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.anchoredPosition = new Vector2(0f, -56f);
        hr.sizeDelta = new Vector2(1300f, 96f);

        headerLabel = MakeText(headerRoot.transform, "HeaderText", 56, Color.white,
            TextAnchor.MiddleCenter, false, true);
        Stretch(headerLabel.rectTransform, 0f);
        headerLabel.text = "";

        var barOuter = MakeImage(canvas.transform, "TimerOuter", outerColor);
        var bor = barOuter.rectTransform;
        bor.anchorMin = new Vector2(0.5f, 1f);
        bor.anchorMax = new Vector2(0.5f, 1f);
        bor.pivot = new Vector2(0.5f, 1f);
        bor.anchoredPosition = new Vector2(0f, -150f);
        bor.sizeDelta = new Vector2(timerBarWidth + 12f, 30f);

        var barBg = MakeImage(barOuter.transform, "TimerBg", new Color(0.12f, 0.13f, 0.18f, 1f));
        Stretch(barBg.rectTransform, 5f);

        var barFill = MakeImage(barBg.transform, "TimerFill", new Color(0.4f, 0.9f, 1f, 1f));
        timerFillRect = barFill.rectTransform;
        timerFillRect.anchorMin = new Vector2(0f, 0.5f);
        timerFillRect.anchorMax = new Vector2(0f, 0.5f);
        timerFillRect.pivot = new Vector2(0f, 0.5f);
        timerFillRect.anchoredPosition = new Vector2(0f, 0f);
        timerFillRect.sizeDelta = new Vector2(timerBarWidth - 4f, 16f);
    }

    private void BuildDeckVisual()
    {
        Vector2 deckPos = new Vector2(-deckCorner.x, deckCorner.y);
        Vector2 deckSize = cardSize * 0.5f;
        for (int i = 0; i < 5; i++)
        {
            var d = MakeImage(canvas.transform, $"DeckCard_{i}", outerColor);
            var drt = d.rectTransform;
            drt.anchorMin = new Vector2(1f, 0f);
            drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = deckPos + new Vector2(i * 4f, i * 5f);
            drt.sizeDelta = deckSize;
            drt.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var face = MakeImage(d.transform, "Face", new Color(0.16f, 0.2f, 0.34f, 1f));
            Stretch(face.rectTransform, 5f);
        }
    }

    public void SetHeader(string text, Color color)
    {
        if (headerLabel == null) return;
        headerLabel.text = text;
        headerLabel.color = color;
    }

    public void SetTimer(float ratio01)
    {
        if (timerFillRect == null) return;
        float r = Mathf.Clamp01(ratio01);
        timerFillRect.sizeDelta = new Vector2((timerBarWidth - 4f) * r, 16f);
        var img = timerFillRect.GetComponent<Image>();
        if (img != null)
        {
            img.color = r > 0.33f
                ? new Color(0.4f, 0.9f, 1f, 1f)
                : new Color(1f, 0.42f, 0.38f, 1f);
        }
    }

    private CardSlot BuildCardSlot(CardInstance card, int index, int total)
    {
        var data = card.Data;
        float step = cardSize.x + cardSpacing;
        float rightmostX = -(rightMargin + cardSize.x * 0.5f);
        float xPos = rightmostX - (total - 1 - index) * step;
        float yPos = bottomOffset;

        var root = new GameObject($"Card_{index}", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(xPos, yPos);
        rt.sizeDelta = cardSize;

        var glow = MakeImage(root.transform, "Glow", new Color(data.cardColor.r, data.cardColor.g, data.cardColor.b, 0f));
        var glowRt = glow.rectTransform;
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = new Vector2(-16f, -16f);
        glowRt.offsetMax = new Vector2(16f, 16f);

        // 階層別フレーム(基本/特殊/必殺)。同心バンドを root 直下に重ねる。
        var frame = ResolveFrame(data.tier, data.cardColor);
        Image panel = null;
        for (int li = 0; li < frame.cols.Length; li++)
        {
            var layer = MakeImage(root.transform, $"Frame_{li}", frame.cols[li]);
            Stretch(layer.rectTransform, frame.insets[li]);
            panel = layer; // 最内 = パネル(コンテンツ親)
        }
        var panelTr = panel.transform;

        // 必殺など: 四隅の飾りスタッド
        if (frame.corner.a > 0f)
        {
            Vector2[] cpos = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            foreach (var cp in cpos)
            {
                var stud = MakeImage(root.transform, "Stud", frame.corner);
                var srt = stud.rectTransform;
                srt.anchorMin = cp;
                srt.anchorMax = cp;
                srt.pivot = cp;
                srt.anchoredPosition = new Vector2(cp.x < 0.5f ? 5f : -5f, cp.y < 0.5f ? 5f : -5f);
                srt.sizeDelta = new Vector2(16f, 16f);
            }
        }

        // スキャンライン(パネル直上, テキスト背面)
        var scan = MakeImage(panelTr, "Scanline", Color.white);
        Stretch(scan.rectTransform, 0f);
        scan.sprite = ScanlineSprite(cardSize, scanlineStrength);
        scan.type = Image.Type.Simple;

        // 名前帯
        var band = MakeImage(panelTr, "NameBand", data.cardColor);
        var bandRt = band.rectTransform;
        bandRt.anchorMin = new Vector2(0f, 0.80f);
        bandRt.anchorMax = new Vector2(1f, 1f);
        bandRt.offsetMin = new Vector2(4f, 4f);
        bandRt.offsetMax = new Vector2(-4f, -4f);
        var nameTxt = MakeText(band.transform, "Name", 30, Color.white,
            TextAnchor.MiddleCenter, false, true);
        Stretch(nameTxt.rectTransform, 2f);
        nameTxt.text = string.IsNullOrEmpty(data.displayName) ? data.name : data.displayName;

        // 階層 + 強化
        var tierTxt = MakeText(panelTr, "Tier", 19, data.cardColor,
            TextAnchor.MiddleCenter, false, true);
        var tierRt = tierTxt.rectTransform;
        tierRt.anchorMin = new Vector2(0f, 0.695f);
        tierRt.anchorMax = new Vector2(1f, 0.795f);
        tierRt.offsetMin = new Vector2(8f, 0f);
        tierRt.offsetMax = new Vector2(-8f, 0f);
        string tl = TierLabel(data.tier);
        tierTxt.text = card.UpgradeLevel > 0 ? $"《{tl}》 ★{card.UpgradeLevel}" : $"《{tl}》";

        bool hasIcon = data.icon != null;
        if (hasIcon)
        {
            var icon = MakeImage(panelTr, "Icon", Color.white);
            var ir = icon.rectTransform;
            ir.anchorMin = new Vector2(0.5f, 0.66f);
            ir.anchorMax = new Vector2(0.5f, 0.66f);
            ir.pivot = new Vector2(0.5f, 1f);
            ir.anchoredPosition = Vector2.zero;
            ir.sizeDelta = new Vector2(74f, 74f);
            icon.sprite = data.icon;
        }

        // 区切り(レトロのダッシュ風)
        var divTxt = MakeText(panelTr, "Div", 16, new Color(1f, 1f, 1f, 0.35f),
            TextAnchor.MiddleCenter, false, false);
        var divRt = divTxt.rectTransform;
        divRt.anchorMin = new Vector2(0f, 0.64f);
        divRt.anchorMax = new Vector2(1f, 0.7f);
        divRt.offsetMin = new Vector2(10f, 0f);
        divRt.offsetMax = new Vector2(-10f, 0f);
        divTxt.text = "- - - - - - - -";

        // 説明
        var descTxt = MakeText(panelTr, "Description", 18, new Color(0.93f, 0.95f, 1f, 1f),
            TextAnchor.UpperCenter, true, false);
        var descRt = descTxt.rectTransform;
        descRt.anchorMin = new Vector2(0f, 0.16f);
        descRt.anchorMax = new Vector2(1f, hasIcon ? 0.55f : 0.64f);
        descRt.offsetMin = new Vector2(12f, 4f);
        descRt.offsetMax = new Vector2(-12f, -2f);
        descTxt.text = data.description;

        // ステータス行(難度のみ。"力/power" は仕様に無いので非表示)
        int eff = card.EffectiveDifficulty;
        var difTxt = MakeText(panelTr, "Difficulty", 26,
            (eff >= 0) ? new Color(1f, 0.6f, 0.25f, 1f) : new Color(0.4f, 0.85f, 1f, 1f),
            TextAnchor.MiddleCenter, false, true);
        var difRt = difTxt.rectTransform;
        difRt.anchorMin = new Vector2(0f, 0.02f);
        difRt.anchorMax = new Vector2(1f, 0.15f);
        difRt.offsetMin = new Vector2(12f, 0f);
        difRt.offsetMax = new Vector2(-12f, 0f);
        difTxt.text = (eff >= 0) ? $"難易度 +{eff}" : $"難易度 {eff}";

        return new CardSlot
        {
            root = rt,
            glow = glow,
            basePosition = new Vector2(xPos, yPos),
            card = card,
        };
    }

    private static string TierLabel(CardTier tier)
    {
        switch (tier)
        {
            case CardTier.Basic: return "基本";
            case CardTier.Special: return "特殊";
            case CardTier.Ultimate: return "必殺";
            default: return "";
        }
    }

    private struct FrameSpec
    {
        public Color[] cols;   // 外→内(最内=パネル)
        public float[] insets; // 各層のカード端からの inset
        public Color corner;   // 四隅スタッド色(a=0 で無し)
    }

    // 階層ごとにフレームの層構成を変える。
    //  基本 : 黒縁 + カテゴリ色(やや暗)細枠 + パネル … 素朴
    //  特殊 : 黒縁 + カテゴリ色(明)太枠 + 白アクセント内枠 + パネル … 装飾的
    //  必殺 : 黒縁 + 金多重枠(金/暗金/金) + 暖色パネル + 四隅スタッド … 最も豪華
    private FrameSpec ResolveFrame(CardTier tier, Color baseCol)
    {
        Color black = outerColor;
        switch (tier)
        {
            case CardTier.Special:
                return new FrameSpec
                {
                    cols = new[] { black, Brighten(baseCol, 0.3f), new Color(1f, 1f, 1f, 0.9f), panelColor },
                    insets = new[] { 0f, 5f, 11f, 15f },
                    corner = new Color(0f, 0f, 0f, 0f),
                };
            case CardTier.Ultimate:
            {
                Color gold = new Color(1f, 0.82f, 0.25f, 1f);
                Color goldDark = new Color(0.42f, 0.30f, 0.05f, 1f);
                Color warm = new Color(0.10f, 0.07f, 0.11f, 1f);
                return new FrameSpec
                {
                    cols = new[] { black, gold, goldDark, gold, warm },
                    insets = new[] { 0f, 6f, 12f, 15f, 19f },
                    corner = gold,
                };
            }
            default: // Basic
                return new FrameSpec
                {
                    cols = new[] { black, Dim(baseCol, 0.25f), panelColor },
                    insets = new[] { 0f, 4f, 10f },
                    corner = new Color(0f, 0f, 0f, 0f),
                };
        }
    }

    private static Color Dim(Color c, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color(c.r * (1f - t), c.g * (1f - t), c.b * (1f - t), c.a);
    }

    private static Color Brighten(Color c, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color(c.r + (1f - c.r) * t, c.g + (1f - c.g) * t, c.b + (1f - c.b) * t, c.a);
    }

    public void MoveCursor(int delta)
    {
        if (IsLocked || slots.Count == 0) return;
        int prev = CursorIndex;
        CursorIndex = Mod(CursorIndex + delta, slots.Count);
        if (CursorIndex != prev)
        {
            ApplyHighlight();
            CursorMoved?.Invoke(CursorIndex);
        }
    }

    public void Confirm()
    {
        if (IsLocked || slots.Count == 0) return;
        IsLocked = true;
        CardConfirmed?.Invoke(CursorIndex);
    }

    private void ApplyHighlight()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            bool on = (i == CursorIndex);
            float scale = on ? highlightScale : 1f;
            s.root.localScale = new Vector3(scale, scale, 1f);
            s.root.anchoredPosition = s.basePosition + new Vector2(0f, on ? highlightLift : 0f);
            s.root.localRotation = Quaternion.identity;
            if (s.glow != null)
            {
                var c = s.glow.color;
                c.a = on ? 0.55f : 0f;
                s.glow.color = c;
            }
            if (on) s.root.SetAsLastSibling();
        }
    }

    // 右下の山札から1枚ずつ弧を描いて配られ、横一列に並ぶ演出
    private IEnumerator AnimateSlideIn()
    {
        int count = slots.Count;
        var startPos = new Vector2[count];
        var endPos = new Vector2[count];
        Vector2 deckOrigin = new Vector2(-deckCorner.x, deckCorner.y);

        for (int i = 0; i < count; i++)
        {
            var s = slots[i];
            endPos[i] = s.basePosition;
            startPos[i] = deckOrigin;
            s.root.anchoredPosition = startPos[i];
            s.root.localRotation = Quaternion.Euler(0f, 0f, -28f);
            s.root.localScale = new Vector3(0.42f, 0.42f, 1f);
            s.root.gameObject.SetActive(false);
        }

        float startTime = Time.unscaledTime;
        bool allDone = false;
        while (!allDone)
        {
            allDone = true;
            float now = Time.unscaledTime - startTime;
            for (int i = 0; i < count; i++)
            {
                float local = now - i * slideInStagger;
                if (local <= 0f) { allDone = false; continue; }
                var s = slots[i];
                if (!s.root.gameObject.activeSelf) s.root.gameObject.SetActive(true);

                float p = Mathf.Clamp01(local / slideInDuration);
                float pop = EaseOutBack(p);
                float sce = EaseOutCubic(p);
                Vector2 flat = Vector2.LerpUnclamped(startPos[i], endPos[i], pop);
                float arc = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) * 140f;
                s.root.anchoredPosition = flat + new Vector2(0f, arc);
                s.root.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-28f, 0f, sce));
                float sc = Mathf.Lerp(0.42f, 1f, sce);
                s.root.localScale = new Vector3(sc, sc, 1f);
                if (p < 1f) allDone = false;
            }
            yield return null;
        }

        ApplyHighlight();
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = x - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    public void Clear()
    {
        StopAllCoroutines();
        slots.Clear();
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
            canvas = null;
        }
        IsLocked = false;
        CursorIndex = 0;
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    // ---- スキャンライン Sprite(カードサイズ毎にキャッシュ) ----
    private static readonly Dictionary<int, Sprite> scanCache = new Dictionary<int, Sprite>();
    private static Sprite ScanlineSprite(Vector2 size, float strength)
    {
        int h = Mathf.Clamp(Mathf.RoundToInt(size.y), 32, 1024);
        int key = h * 1000 + Mathf.RoundToInt(Mathf.Clamp01(strength) * 100f);
        if (scanCache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int w = 8;
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Point;
        byte a = (byte)(Mathf.Clamp01(strength) * 255f);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            byte la = (y % 3 == 0) ? a : (byte)0;
            for (int x = 0; x < w; x++) px[y * w + x] = new Color32(0, 0, 0, la);
        }
        tex.SetPixels32(px);
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
        scanCache[key] = sp;
        return sp;
    }

    private class CardSlot
    {
        public RectTransform root;
        public Image glow;
        public Vector2 basePosition;
        public CardInstance card;
    }
}
