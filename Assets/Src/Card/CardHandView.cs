using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手札 UI ビュー（8bit ピクセル風）。
/// OnGUI ベースで描画し、PixelFontRenderer を使用。
/// カーソル移動と確定を管理する。
/// </summary>
public class CardHandView : MonoBehaviour
{
    [Tooltip("カード1枚のサイズ(px)。")]
    public Vector2 cardSize = new Vector2(200f, 280f);
    [Tooltip("横方向のカード間隔(px, カード端の隙間)。")]
    public float cardSpacing = 20f;
    [Tooltip("カード列の右端から画面右端までの余白(px)。")]
    public float rightMargin = 80f;
    [Tooltip("画面下からのカード中心オフセット(px)。")]
    public float bottomOffset = 180f;
    [Tooltip("選択中カードの拡大倍率。")]
    public float highlightScale = 1.12f;
    [Tooltip("選択中カードの上への迫り出し量(px)。")]
    public float highlightLift = 40f;
    [Tooltip("タイマーバーの幅(px)。")]
    public float timerBarWidth = 600f;

    [Header("8bit配色（GameColorScheme ベース）")]
    [Tooltip("カード背景色(暗紫)。")]
    public Color panelColor = new Color(0.12f, 0.05f, 0.18f, 1f);    // PrimaryDark
    [Tooltip("カード枠線色(紫)。")]
    public Color borderColor = new Color(0.5f, 0.35f, 0.6f, 1f);     // FrameOuter
    [Tooltip("選択中の枠線色(黄)。")]
    public Color selectedBorderColor = new Color(1f, 0.9f, 0.3f, 1f); // AccentBright
    [Tooltip("ヘッダー背景色(暗紫)。")]
    public Color headerBgColor = new Color(0.1f, 0.04f, 0.14f, 0.9f);

    /// <summary>カーソルが移動したときに発火(新しい index)。</summary>
    public event Action<int> CursorMoved;
    /// <summary>カードが確定された瞬間に発火(確定 index)。</summary>
    public event Action<int> CardConfirmed;

    public int CursorIndex { get; private set; } = 0;
    public bool IsLocked { get; private set; } = false;
    public int CardCount => cards.Count;

    private readonly List<CardInstance> cards = new List<CardInstance>();
    private string headerText = "";
    private Color headerColor = Color.white;
    private float timerRatio = 1f;

    // ピクセルテクスチャ
    private Texture2D cardBgTexture;
    private Texture2D cardSelectedBgTexture;
    private Texture2D headerBgTexture;
    private Texture2D timerBgTexture;
    private Texture2D timerFillTexture;
    private Texture2D timerDangerTexture;
    private Texture2D headerLabelTexture;
    private readonly Dictionary<string, Texture2D> textCache = new Dictionary<string, Texture2D>();

    // 点滅制御
    private float blinkTimer;
    private bool blinkOn = true;

    // アニメーション
    private float buildTime = -1f;
    private const float SLIDE_DURATION = 0.4f;
    private const float SLIDE_STAGGER = 0.08f;

    public void Build(IList<CardInstance> newCards)
    {
        Clear();
        if (newCards == null || newCards.Count == 0) return;

        foreach (var c in newCards)
        {
            cards.Add(c);
        }

        CursorIndex = 0;
        IsLocked = false;
        buildTime = Time.unscaledTime;
        CreateTextures();
    }

    private void CreateTextures()
    {
        // カード背景
        cardBgTexture = PixelFontRenderer.CreatePixelBox(
            (int)cardSize.x / 4, (int)cardSize.y / 4,
            panelColor, borderColor, 2);

        cardSelectedBgTexture = PixelFontRenderer.CreatePixelBox(
            (int)cardSize.x / 4, (int)cardSize.y / 4,
            panelColor, selectedBorderColor, 3);

        // ヘッダー背景（実際の表示サイズに合わせる）
        headerBgTexture = PixelFontRenderer.CreatePixelBox(
            200, 16, headerBgColor, borderColor, 2);

        // タイマーバー
        timerBgTexture = PixelFontRenderer.CreatePixelBox(
            (int)timerBarWidth / 4, 6,
            new Color(0.15f, 0.15f, 0.2f, 0.9f),
            borderColor, 1);

        timerFillTexture = CreateSolidTexture(new Color(0.3f, 0.85f, 1f, 1f));
        timerDangerTexture = CreateSolidTexture(new Color(1f, 0.4f, 0.35f, 1f));
    }

    private Texture2D CreateSolidTexture(Color color)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D GetTextTexture(string text, Color color, int scale = 3)
    {
        string key = $"{text}_{color.r:F2}_{color.g:F2}_{color.b:F2}_{scale}";
        if (textCache.TryGetValue(key, out var cached) && cached != null)
        {
            return cached;
        }
        var tex = PixelFontRenderer.RenderText(text, color, scale);
        textCache[key] = tex;
        return tex;
    }

    // 日本語対応テキスト描画用スタイル
    private GUIStyle cardTextStyle;
    private GUIStyle cardDescStyle;
    private Font cachedMisakiFont;

    private void EnsureCardTextStyles()
    {
        if (cardTextStyle != null) return;

        cachedMisakiFont = Resources.Load<Font>("Fonts/misaki_gothic");

        cardTextStyle = new GUIStyle(GUI.skin.label);
        cardTextStyle.alignment = TextAnchor.MiddleCenter;
        cardTextStyle.fontStyle = FontStyle.Bold;
        if (cachedMisakiFont != null) cardTextStyle.font = cachedMisakiFont;

        cardDescStyle = new GUIStyle(GUI.skin.label);
        cardDescStyle.alignment = TextAnchor.UpperCenter;
        cardDescStyle.wordWrap = true;
        if (cachedMisakiFont != null) cardDescStyle.font = cachedMisakiFont;
    }

    private void DrawTextLabel(string text, Rect rect, Color color, int fontSize, bool centered = true)
    {
        EnsureCardTextStyles();
        var style = centered ? cardTextStyle : cardDescStyle;
        style.fontSize = fontSize;
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }

    private void DestroyTextures()
    {
        if (cardBgTexture != null) Destroy(cardBgTexture);
        if (cardSelectedBgTexture != null) Destroy(cardSelectedBgTexture);
        if (headerBgTexture != null) Destroy(headerBgTexture);
        if (timerBgTexture != null) Destroy(timerBgTexture);
        if (timerFillTexture != null) Destroy(timerFillTexture);
        if (timerDangerTexture != null) Destroy(timerDangerTexture);
        if (headerLabelTexture != null) Destroy(headerLabelTexture);

        foreach (var kv in textCache)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        textCache.Clear();
    }

    public void SetHeader(string text, Color color)
    {
        headerText = text;
        headerColor = color;
        // 日本語対応: テクスチャは OnGUI 内で遅延生成
        needsHeaderTextureUpdate = true;
    }

    private bool needsHeaderTextureUpdate = false;

    public void SetTimer(float ratio01)
    {
        timerRatio = Mathf.Clamp01(ratio01);
    }

    public void MoveCursor(int delta)
    {
        if (IsLocked || cards.Count == 0) return;
        int prev = CursorIndex;
        CursorIndex = Mod(CursorIndex + delta, cards.Count);
        if (CursorIndex != prev)
        {
            CursorMoved?.Invoke(CursorIndex);
        }
    }

    public void Confirm()
    {
        if (IsLocked || cards.Count == 0) return;
        IsLocked = true;
        CardConfirmed?.Invoke(CursorIndex);
    }

    public void Clear()
    {
        cards.Clear();
        DestroyTextures();
        IsLocked = false;
        CursorIndex = 0;
        buildTime = -1f;
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    void Update()
    {
        // 選択カードの点滅
        blinkTimer += Time.deltaTime;
        if (blinkTimer >= 0.25f)
        {
            blinkTimer = 0f;
            blinkOn = !blinkOn;
        }
    }

    void OnGUI()
    {
        if (cards.Count == 0) return;

        DrawHeader();
        DrawTimer();
        DrawCards();
    }

    private GUIStyle headerStyle;

    private void DrawHeader()
    {
        if (string.IsNullOrEmpty(headerText)) return;

        float headerWidth = 800f;
        float headerHeight = 64f;
        float x = (Screen.width - headerWidth) / 2f;
        float y = 50f;

        // 背景
        if (headerBgTexture != null)
        {
            GUI.DrawTexture(new Rect(x, y, headerWidth, headerHeight), headerBgTexture, ScaleMode.StretchToFill);
        }

        // GUIスタイルで日本語を直接描画
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 28;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            // 美咲フォントをロード
            var misakiFont = Resources.Load<Font>("Fonts/misaki_gothic");
            if (misakiFont != null)
            {
                headerStyle.font = misakiFont;
                headerStyle.fontSize = 24;
            }
        }

        headerStyle.normal.textColor = headerColor;
        GUI.Label(new Rect(x, y, headerWidth, headerHeight), headerText, headerStyle);
    }

    private void DrawTimer()
    {
        float barHeight = 24f;
        float x = (Screen.width - timerBarWidth) / 2f;
        float y = 130f;

        // 背景
        GUI.DrawTexture(new Rect(x, y, timerBarWidth, barHeight), timerBgTexture, ScaleMode.StretchToFill);

        // 残量バー
        float padding = 4f;
        float fillWidth = (timerBarWidth - padding * 2) * timerRatio;

        if (fillWidth > 0)
        {
            Texture2D barTex = (timerRatio > 0.33f) ? timerFillTexture : timerDangerTexture;
            GUI.DrawTexture(
                new Rect(x + padding, y + padding, fillWidth, barHeight - padding * 2),
                barTex, ScaleMode.StretchToFill);
        }
    }

    private void DrawCards()
    {
        int count = cards.Count;
        float step = cardSize.x + cardSpacing;
        float rightmostX = Screen.width - rightMargin - cardSize.x / 2f;
        float baseY = Screen.height - bottomOffset;

        float elapsed = (buildTime > 0) ? Time.unscaledTime - buildTime : 999f;

        for (int i = 0; i < count; i++)
        {
            float cardX = rightmostX - (count - 1 - i) * step;
            float cardY = baseY;
            float scale = 1f;

            // スライドインアニメーション
            float localTime = elapsed - i * SLIDE_STAGGER;
            float animProgress = Mathf.Clamp01(localTime / SLIDE_DURATION);

            if (animProgress < 1f)
            {
                // 右から左へスライド
                float startX = Screen.width + cardSize.x;
                cardX = Mathf.Lerp(startX, cardX, EaseOutCubic(animProgress));
                scale = Mathf.Lerp(0.5f, 1f, animProgress);
            }

            bool isSelected = (i == CursorIndex);

            // 選択中は拡大 + 上に浮く
            if (isSelected && animProgress >= 1f)
            {
                scale = highlightScale;
                cardY -= highlightLift;
            }

            DrawCard(cards[i], cardX, cardY, scale, isSelected);
        }
    }

    private void DrawCard(CardInstance card, float centerX, float centerY, float scale, bool isSelected)
    {
        if (card == null || card.Data == null) return;

        float w = cardSize.x * scale;
        float h = cardSize.y * scale;
        float x = centerX - w / 2f;
        float y = centerY - h / 2f;

        // 選択中のスポットライト・グロー枠
        if (isSelected)
        {
            float glowPad = 6f * scale;
            Texture2D glowTex = cardSelectedBgTexture;
            if (glowTex != null)
            {
                Color prevCol = GUI.color;
                GUI.color = blinkOn ? new Color(1f, 0.95f, 0.4f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.5f);
                GUI.DrawTexture(new Rect(x - glowPad, y - glowPad, w + glowPad * 2, h + glowPad * 2), glowTex, ScaleMode.StretchToFill);
                GUI.color = prevCol;
            }
        }

        // TCGデザインカードテクスチャの描画
        Texture2D cardTex = CardVisualGenerator.CreateCardTexture(card, isSelected);
        if (cardTex != null)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), cardTex, ScaleMode.StretchToFill);
        }
    }

    private void DrawMultilineText(string text, float x, float startY, float maxWidth, float scale, Color color)
    {
        // 簡易的な折り返し処理
        int charsPerLine = Mathf.Max(6, (int)(maxWidth / (8 * scale * 2))); // 8px per char at scale 2
        float lineHeight = 20f * scale;
        float currentY = startY;

        int pos = 0;
        while (pos < text.Length && currentY < startY + 100f * scale)
        {
            int len = Mathf.Min(charsPerLine, text.Length - pos);
            string line = text.Substring(pos, len);
            var lineTex = GetTextTexture(line, color, 2);
            if (lineTex != null)
            {
                float lineX = x + (maxWidth - lineTex.width) / 2f;
                GUI.DrawTexture(new Rect(lineX, currentY, lineTex.width, lineTex.height), lineTex);
            }
            currentY += lineHeight;
            pos += len;
        }
    }

    private static string GetTierLabel(CardTier tier)
    {
        switch (tier)
        {
            case CardTier.Basic: return "BASIC";
            case CardTier.Special: return "SPECIAL";
            case CardTier.Ultimate: return "ULTIMATE";
            default: return "";
        }
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
