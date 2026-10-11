using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本格的なTCG（トレーディングカードゲーム）デザインのカードテクスチャおよびスプライトを生成するジェネレータ。
/// 劇場・舞台×8bitの「アンダーザスポットライト」テーマに合わせた豪華でオシャレなカード枠、
/// コスト/難易度ジュエル、美麗なカテゴリ別ピクセルエンブレム、タイプ・範囲リボン、
/// 効果説明ボックス、パワーメダリオン、カード裏面をプロシージャルに描画・キャッシュします。
/// </summary>
public static class CardVisualGenerator
{
    public const int CARD_WIDTH = 300;
    public const int CARD_HEIGHT = 420;

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static Sprite cachedCardBackSprite;
    private static Texture2D cachedCardBackTexture;

    // フォント
    private static Font cachedFont;

    private static Font GetCardFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.Load<Font>("Fonts/misaki_gothic");
        if (cachedFont == null) cachedFont = GameFont.Body();
        return cachedFont;
    }

    /// <summary>
    /// 指定されたカードインスタンスから、洗練されたTCGスタイルのSpriteを取得（キャッシュ付き）
    /// </summary>
    public static Sprite GetCardSprite(CardInstance card)
    {
        if (card == null || card.Data == null) return null;

        string key = $"{card.Data.id}_{card.UpgradeLevel}_{card.EffectiveDifficulty}_{card.EffectivePower}";
        if (spriteCache.TryGetValue(key, out var cached) && cached != null)
        {
            return cached;
        }

        Texture2D tex = CreateCardTexture(card);
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, CARD_WIDTH, CARD_HEIGHT),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = $"CardSprite_{key}";
        spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// カード裏面の豪華な劇場デザインSpriteを取得
    /// </summary>
    public static Sprite GetCardBackSprite()
    {
        if (cachedCardBackSprite != null) return cachedCardBackSprite;

        Texture2D tex = GetCardBackTexture();
        cachedCardBackSprite = Sprite.Create(
            tex,
            new Rect(0, 0, CARD_WIDTH, CARD_HEIGHT),
            new Vector2(0.5f, 0.5f),
            100f
        );
        cachedCardBackSprite.name = "CardBackSprite";
        return cachedCardBackSprite;
    }

    /// <summary>
    /// カード裏面Texture2Dを取得
    /// </summary>
    public static Texture2D GetCardBackTexture()
    {
        if (cachedCardBackTexture != null) return cachedCardBackTexture;

        cachedCardBackTexture = CreateCardBackTexture();
        return cachedCardBackTexture;
    }

    /// <summary>
    /// カードのTexture2Dを生成（OnGUI / UI描画用）
    /// </summary>
    public static Texture2D CreateCardTexture(CardInstance card, bool isSelected = false)
    {
        if (card == null || card.Data == null) return null;

        string key = $"{card.Data.id}_{card.UpgradeLevel}_{card.EffectiveDifficulty}_{card.EffectivePower}_{(isSelected ? "sel" : "norm")}";
        if (textureCache.TryGetValue(key, out var cached) && cached != null)
        {
            return cached;
        }

        var data = card.Data;
        int w = CARD_WIDTH;
        int h = CARD_HEIGHT;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        // シンプルで読みやすいカード面(名前・種別・難度・効果のみ、装飾は最小限)
        DrawSimpleCard(px, w, h, card);

        // ピクセル書き込み
        tex.SetPixels(px);
        tex.Apply();

        textureCache[key] = tex;
        return tex;
    }

    // =========================================================================
    // ピクセル描画ヘルパー
    // =========================================================================

    /// <summary>
    /// シンプルで見やすいカード面。上に名前バンド、その下に「種別」「難度」を1行ずつ、
    /// 仕切り線、効果文(折り返し)。要素は重ならない。色はタイプ色＋クリーム＋濃色の最小限。
    /// (座標系は y=0 が下, y=h が上)
    /// </summary>
    private static void DrawSimpleCard(Color[] px, int w, int h, CardInstance card)
    {
        var data = card.Data;
        Color type = GetCategoryColor(data.category);
        Color band = type;
        Color border = type;
        Color body = new Color(0.11f, 0.085f, 0.16f, 1f);   // ダーク下地(背景に馴染む)
        Color textLight = new Color(0.94f, 0.92f, 0.99f, 1f);

        // 全面をダーク下地で塗る
        FillRectPx(px, w, h, 0, 0, w, h, body);

        // 外枠(タイプ色)
        int b = 6;
        FillRectPx(px, w, h, 0, 0, w, b, border);
        FillRectPx(px, w, h, 0, h - b, w, b, border);
        FillRectPx(px, w, h, 0, 0, b, h, border);
        FillRectPx(px, w, h, w - b, 0, b, h, border);

        int mx = 20; // 左右マージン
        int availW = w - mx * 2;

        // 上部: 名前バンド(タイプ色・白文字)。名前は幅に合わせて最大サイズを選ぶ。
        int bandH = 76;
        int bandBottom = h - b - bandH;
        FillRectPx(px, w, h, b, bandBottom, w - 2 * b, bandH, band);
        string nameStr = string.IsNullOrEmpty(data.displayName) ? data.name : data.displayName;
        int nameScale = FitScale(nameStr, availW, 6, 4);
        DrawStringOnPixels(px, w, h, mx + 4, bandBottom + (bandH - 8 * nameScale) / 2, nameStr, Color.white, nameScale);

        // 種別 / 難度 (1行ずつ・大きめ)。幅に収まる最大スケールを選ぶ。
        int diff = card.EffectiveDifficulty;
        string catStr = "種別  " + GetCategoryNameJp(data.category);
        string diffStr = "難度  " + (diff >= 0 ? $"+{diff}" : $"{diff}");
        int infoScale = Mathf.Min(FitScale(catStr, availW, 4, 3), FitScale(diffStr, availW, 4, 3));
        DrawStringOnPixels(px, w, h, mx, bandBottom - 44, catStr, textLight, infoScale);
        DrawStringOnPixels(px, w, h, mx, bandBottom - 82, diffStr, textLight, infoScale);

        // 仕切り線(タイプ色)
        int lineY = bandBottom - 98;
        FillRectPx(px, w, h, mx, lineY, availW, 2, Color.Lerp(type, body, 0.25f));

        // 効果文: 残りの領域に収まる最大サイズを選んで折り返し描画(はみ出さない)
        string descStr = data.description ?? "";
        int descTop = lineY - 8;          // 効果テキスト領域の上端
        int descBottom = b + 10;          // 下端(枠の内側)
        int descAvailH = descTop - descBottom;
        int descScale = FitWrappedScale(descStr, availW, descAvailH, 5, 3);
        DrawWrappedStringOnPixels(px, w, h, mx, descTop - 8 * descScale, availW, descStr, textLight, descScale);
    }

    /// <summary>行送り(px)。1行の高さ。</summary>
    private static int LineHeight(int scale) => (PixelFontRenderer.JP_CHAR_HEIGHT + 3) * scale;

    /// <summary>幅 availW に収まる最大の文字スケール(実測幅で判定)。</summary>
    private static int FitScale(string s, int availW, int maxScale, int minScale)
    {
        if (string.IsNullOrEmpty(s)) return maxScale;
        for (int sc = maxScale; sc > minScale; sc--)
            if (PixelFontRenderer.MeasureWidth(s, sc) <= availW) return sc;
        return minScale;
    }

    /// <summary>幅と高さの両方に収まる最大の文字スケール(実測幅で折り返して高さ判定)。</summary>
    private static int FitWrappedScale(string s, int availW, int availH, int maxScale, int minScale)
    {
        if (string.IsNullOrEmpty(s)) return maxScale;
        for (int sc = maxScale; sc >= minScale; sc--)
        {
            var lines = WrapText(s, availW, sc);
            int th = lines.Count * LineHeight(sc);
            if (th <= availH) return sc;
        }
        return minScale;
    }

    /// <summary>availW に収まるよう、実測幅で貪欲に行へ折り返す。</summary>
    private static List<string> WrapText(string text, int availW, int scale)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;
        int start = 0;
        while (start < text.Length)
        {
            int count = 0;
            while (start + count < text.Length)
            {
                int next = count + 1;
                // 改行文字は明示的な区切り
                if (text[start + count] == '\n') break;
                int wpx = PixelFontRenderer.MeasureWidth(text.Substring(start, next), scale);
                if (wpx > availW && count > 0) break;
                count = next;
            }
            if (count == 0)
            {
                // 改行のみ、または1文字も入らない場合
                if (text[start] == '\n') { lines.Add(string.Empty); start++; continue; }
                count = 1;
            }
            lines.Add(text.Substring(start, count));
            start += count;
            if (start < text.Length && text[start] == '\n') start++; // 行末の改行を消費
        }
        return lines;
    }

    /// <summary>矩形をベタ塗り(y=0 が下)。範囲外は自動でクリップ。</summary>
    private static void FillRectPx(Color[] px, int w, int h, int x, int y, int rw, int rh, Color c)
    {
        int x0 = Mathf.Max(0, x), x1 = Mathf.Min(w, x + rw);
        int y0 = Mathf.Max(0, y), y1 = Mathf.Min(h, y + rh);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                px[yy * w + xx] = c;
    }

    private static void DrawCardChassis(Color[] px, int w, int h, CardTier tier, CardCategory cat, bool isSelected)
    {
        // ティア別テーマカラー
        Color frameGold = isSelected ? new Color(1f, 0.95f, 0.4f, 1f) :
            (tier == CardTier.Ultimate ? new Color(1.0f, 0.82f, 0.25f, 1f) :
             tier == CardTier.Special  ? new Color(0.75f, 0.55f, 0.95f, 1f) :
                                         new Color(0.68f, 0.52f, 0.38f, 1f));

        Color frameTrim = tier == CardTier.Ultimate ? new Color(1f, 0.92f, 0.55f, 1f) :
                          tier == CardTier.Special  ? new Color(0.9f, 0.78f, 1f, 1f) :
                                                      new Color(0.85f, 0.72f, 0.58f, 1f);

        Color cardBaseDark = new Color(0.09f, 0.05f, 0.13f, 1f); // 深い劇場紫
        Color innerCardFill = new Color(0.14f, 0.08f, 0.20f, 1f);

        int cornerR = 12;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 角丸判定
                if (IsOutsideRoundedRect(x, y, 0, 0, w, h, cornerR))
                {
                    continue; // カード外側（透明）
                }

                // 最外枠（1px 黒・影）
                if (IsOnRoundedRectEdge(x, y, 0, 0, w, h, cornerR, 1))
                {
                    px[y * w + x] = new Color(0.04f, 0.02f, 0.06f, 0.9f);
                    continue;
                }

                // メタリック外枠（2〜4px）
                if (IsOnRoundedRectEdge(x, y, 1, 1, w - 2, h - 2, cornerR - 1, 3))
                {
                    // 上下左右で光沢グラデーション
                    float lightT = (float)y / h;
                    px[y * w + x] = Color.Lerp(frameGold * 0.7f, frameTrim, lightT);
                    continue;
                }

                // 枠内側の溝（1px 暗色）
                if (IsOnRoundedRectEdge(x, y, 4, 4, w - 8, h - 8, cornerR - 3, 1))
                {
                    px[y * w + x] = new Color(0.06f, 0.03f, 0.08f, 1f);
                    continue;
                }

                // カード本体面
                float bgT = (float)y / h;
                Color baseCol = Color.Lerp(cardBaseDark, innerCardFill, bgT);

                // 繊細なディザパターン（織物/カード紙風）
                if (((x / 2) + (y / 2)) % 2 == 0)
                {
                    baseCol += new Color(0.015f, 0.01f, 0.025f, 0f);
                }

                px[y * w + x] = baseCol;
            }
        }

        // 4隅の飾り鋲（ゴールドリベット）
        DrawCornerRivet(px, w, h, 14, h - 14, frameTrim);
        DrawCornerRivet(px, w, h, w - 14, h - 14, frameTrim);
        DrawCornerRivet(px, w, h, 14, 14, frameTrim);
        DrawCornerRivet(px, w, h, w - 14, 14, frameTrim);
    }

    private static void DrawCornerRivet(Color[] px, int w, int h, int cx, int cy, Color col)
    {
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int distSq = dx * dx + dy * dy;
                if (distSq <= 4)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x >= 0 && x < w && y >= 0 && y < h)
                    {
                        Color c = (dx == -1 && dy == 1) ? Color.white : (distSq == 4 ? col * 0.6f : col);
                        px[y * w + x] = c;
                    }
                }
            }
        }
    }

    private static void DrawArtWindow(Color[] px, int w, int h, RectInt r, CardCategory cat, CardTier tier, Sprite customIcon)
    {
        Color windowBorder = new Color(0.45f, 0.35f, 0.55f, 1f);
        Color windowBorderHi = new Color(0.8f, 0.7f, 0.95f, 1f);
        Color windowInnerBg = new Color(0.07f, 0.04f, 0.11f, 1f);

        // ウィンドウ背景（スポットライトグラデーション）
        int cx = r.x + r.width / 2;
        int cy = r.y + r.height / 2;
        float maxDist = Mathf.Sqrt(r.width * r.width + r.height * r.height) * 0.5f;

        Color catGlow = GetCategoryColor(cat) * 0.45f;

        for (int y = r.y; y < r.y + r.height; y++)
        {
            for (int x = r.x; x < r.x + r.width; x++)
            {
                // 外枠
                if (x == r.x || x == r.x + r.width - 1 || y == r.y || y == r.y + r.height - 1)
                {
                    px[y * w + x] = windowBorder;
                    continue;
                }
                if (x == r.x + 1 || x == r.x + r.width - 2 || y == r.y + 1 || y == r.y + r.height - 2)
                {
                    px[y * w + x] = (y >= r.y + r.height - 2 || x == r.x + 1) ? windowBorderHi : new Color(0.04f, 0.02f, 0.06f, 1f);
                    continue;
                }

                // スポットライト風ラジアルグラデーション
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy + 10));
                float glow = Mathf.Clamp01(1f - (d / (maxDist * 0.75f)));
                glow = glow * glow;

                Color c = Color.Lerp(windowInnerBg, catGlow, glow);
                // 繊細なステージダスト
                if (((x * 13 + y * 19) % 37 == 0) && glow > 0.3f)
                {
                    c += new Color(0.12f, 0.12f, 0.15f, 0f);
                }

                px[y * w + x] = c;
            }
        }

        // エンブレム描画
        if (customIcon != null && customIcon.texture != null)
        {
            BlitSpriteToPixels(px, w, h, customIcon, cx, cy, 64);
        }
        else
        {
            DrawCategoryEmblem(px, w, h, cx, cy, cat, tier);
        }
    }

    private static void DrawCategoryEmblem(Color[] px, int w, int h, int cx, int cy, CardCategory cat, CardTier tier)
    {
        switch (cat)
        {
            case CardCategory.Attack:
                DrawCrossedSwordsEmblem(px, w, h, cx, cy);
                break;
            case CardCategory.Defend:
                DrawShieldEmblem(px, w, h, cx, cy);
                break;
            case CardCategory.Heal:
                DrawHealHeartEmblem(px, w, h, cx, cy);
                break;
            case CardCategory.Buff:
                DrawBuffLightningEmblem(px, w, h, cx, cy);
                break;
            case CardCategory.Special:
                DrawTheatricalMaskEmblem(px, w, h, cx, cy);
                break;
        }

        if (tier == CardTier.Ultimate)
        {
            DrawCrownOverlay(px, w, h, cx, cy + 32);
        }
    }

    // --- 各カテゴリのスタイリッシュピクセルエンブレム ---

    private static void DrawCrossedSwordsEmblem(Color[] px, int w, int h, int cx, int cy)
    {
        Color bladeEdge = new Color(1f, 1f, 1f, 1f);
        Color bladeCore = new Color(0.85f, 0.9f, 1f, 1f);
        Color hiltGold = new Color(1f, 0.8f, 0.2f, 1f);
        Color glowRed = new Color(1f, 0.3f, 0.3f, 0.8f);

        // 剣1（左下から右上） & 剣2（右下から左上）
        for (int i = -24; i <= 24; i++)
        {
            // 剣1
            DrawPixelSafe(px, w, h, cx + i, cy + i, bladeCore);
            DrawPixelSafe(px, w, h, cx + i - 1, cy + i, bladeEdge);
            DrawPixelSafe(px, w, h, cx + i + 1, cy + i, bladeEdge);
            // グロー
            DrawPixelSafe(px, w, h, cx + i - 2, cy + i, glowRed);
            DrawPixelSafe(px, w, h, cx + i + 2, cy + i, glowRed);

            // 剣2
            DrawPixelSafe(px, w, h, cx - i, cy + i, bladeCore);
            DrawPixelSafe(px, w, h, cx - i - 1, cy + i, bladeEdge);
            DrawPixelSafe(px, w, h, cx - i + 1, cy + i, bladeEdge);
            // グロー
            DrawPixelSafe(px, w, h, cx - i - 2, cy + i, glowRed);
            DrawPixelSafe(px, w, h, cx - i + 2, cy + i, glowRed);
        }

        // つば（ガード）
        for (int g = -6; g <= 6; g++)
        {
            DrawPixelSafe(px, w, h, cx - 14 + g, cy - 14 - g, hiltGold);
            DrawPixelSafe(px, w, h, cx + 14 - g, cy - 14 - g, hiltGold);
        }

        // 柄頭（ポンメル）
        DrawPixelSafe(px, w, h, cx - 22, cy - 22, hiltGold);
        DrawPixelSafe(px, w, h, cx + 22, cy - 22, hiltGold);

        // 中央のクロススパーク
        for (int s = -4; s <= 4; s++)
        {
            DrawPixelSafe(px, w, h, cx + s, cy, Color.white);
            DrawPixelSafe(px, w, h, cx, cy + s, Color.white);
        }
    }

    private static void DrawShieldEmblem(Color[] px, int w, int h, int cx, int cy)
    {
        Color shieldRim = new Color(0.4f, 0.75f, 1f, 1f);
        Color shieldBody = new Color(0.15f, 0.35f, 0.65f, 1f);
        Color shieldCrest = new Color(1f, 0.9f, 0.4f, 1f);

        for (int y = -24; y <= 24; y++)
        {
            int span = (y > 0) ? 22 : (int)(22 * (1f + (float)y / 26f));
            span = Mathf.Max(2, span);

            for (int x = -span; x <= span; x++)
            {
                bool isRim = (Mathf.Abs(x) >= span - 2) || (y == 24 || y == 23) || (y < 0 && Mathf.Abs(x) >= span - 2);
                Color c = isRim ? shieldRim : shieldBody;
                if (x == 0 || y == 4) c = shieldCrest; // 中央十字
                DrawPixelSafe(px, w, h, cx + x, cy + y, c);
            }
        }
    }

    private static void DrawHealHeartEmblem(Color[] px, int w, int h, int cx, int cy)
    {
        Color heartFill = new Color(0.2f, 0.85f, 0.45f, 1f);
        Color heartGlow = new Color(0.7f, 1f, 0.8f, 1f);
        Color crossColor = Color.white;

        // ハート形状（数学的近似）
        for (int y = -22; y <= 20; y++)
        {
            for (int x = -24; x <= 24; x++)
            {
                float nx = x / 18f;
                float ny = y / 18f;
                float eq = (nx * nx + ny * ny - 1f);
                if (eq * eq * eq - nx * nx * ny * ny * ny <= 0f)
                {
                    bool isEdge = (eq * eq * eq - nx * nx * ny * ny * ny >= -0.15f);
                    DrawPixelSafe(px, w, h, cx + x, cy + y + 2, isEdge ? heartGlow : heartFill);
                }
            }
        }

        // 白い十字架
        for (int ty = -10; ty <= 10; ty++)
            for (int tx = -2; tx <= 2; tx++)
                DrawPixelSafe(px, w, h, cx + tx, cy + ty + 2, crossColor);

        for (int ty = -2; ty <= 2; ty++)
            for (int tx = -10; tx <= 10; tx++)
                DrawPixelSafe(px, w, h, cx + tx, cy + ty + 2, crossColor);
    }

    private static void DrawBuffLightningEmblem(Color[] px, int w, int h, int cx, int cy)
    {
        Color boltCore = Color.white;
        Color boltEdge = new Color(1f, 0.85f, 0.1f, 1f);
        Color boltGlow = new Color(1f, 0.5f, 0f, 0.8f);

        // 稲妻ジグザグ
        int[] segmentsX = { 10, -4, 4, -12 };
        int[] segmentsY = { 26, 4, 4, -26 };

        // 上部ライン
        DrawLineThick(px, w, h, cx + 8, cy + 24, cx - 2, cy + 2, boltCore, boltEdge, boltGlow);
        // 水平返し
        DrawLineThick(px, w, h, cx - 2, cy + 2, cx + 8, cy + 2, boltCore, boltEdge, boltGlow);
        // 下部ライン
        DrawLineThick(px, w, h, cx + 8, cy + 2, cx - 10, cy - 24, boltCore, boltEdge, boltGlow);
    }

    private static void DrawTheatricalMaskEmblem(Color[] px, int w, int h, int cx, int cy)
    {
        Color maskGold = new Color(1f, 0.85f, 0.35f, 1f);
        Color maskShadow = new Color(0.55f, 0.35f, 0.15f, 1f);
        Color eyeHole = new Color(0.08f, 0.04f, 0.12f, 1f);

        // 楕円マスク
        for (int dy = -22; dy <= 22; dy++)
        {
            int span = (int)(Mathf.Sqrt(Mathf.Max(0, 1f - (dy * dy) / (22f * 22f))) * 20f);
            for (int dx = -span; dx <= span; dx++)
            {
                Color c = (dx < 0) ? maskGold : maskShadow;
                // 目と口
                if ((dy >= 4 && dy <= 10 && (Mathf.Abs(dx - 7) <= 3 || Mathf.Abs(dx + 7) <= 3)) ||
                    (dy >= -12 && dy <= -8 && Mathf.Abs(dx) <= 7 && dy >= -12 + (dx * dx) / 10))
                {
                    c = eyeHole;
                }
                DrawPixelSafe(px, w, h, cx + dx, cy + dy, c);
            }
        }
    }

    private static void DrawCrownOverlay(Color[] px, int w, int h, int cx, int cy)
    {
        Color gold = new Color(1f, 0.9f, 0.2f, 1f);
        Color ruby = new Color(1f, 0.2f, 0.2f, 1f);

        for (int x = -16; x <= 16; x++)
        {
            for (int y = -4; y <= 10; y++)
            {
                int absX = Mathf.Abs(x);
                bool isCrown = (y <= 0) || (absX == 16 && y <= 8) || (absX == 0 && y <= 10) || (absX == 8 && y <= 6);
                if (isCrown)
                {
                    Color c = (y == 10 || y == 8) ? ruby : gold;
                    DrawPixelSafe(px, w, h, cx + x, cy + y, c);
                }
            }
        }
    }

    private static void DrawLineThick(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color core, Color edge, Color glow)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy, e2;

        while (true)
        {
            for (int ox = -2; ox <= 2; ox++)
            {
                for (int oy = -2; oy <= 2; oy++)
                {
                    int d = ox * ox + oy * oy;
                    if (d <= 1) DrawPixelSafe(px, w, h, x0 + ox, y0 + oy, core);
                    else if (d <= 4) DrawPixelSafe(px, w, h, x0 + ox, y0 + oy, edge);
                    else if (d <= 8) DrawPixelSafe(px, w, h, x0 + ox, y0 + oy, glow);
                }
            }
            if (x0 == x1 && y0 == y1) break;
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static void DrawTypeRibbon(Color[] px, int w, int h, RectInt r, CardCategory cat, CardAttackRange range)
    {
        Color ribbonBg = new Color(0.10f, 0.06f, 0.15f, 1f);
        Color ribbonBorder = new Color(0.5f, 0.38f, 0.6f, 1f);
        Color catBadgeCol = GetCategoryColor(cat);

        for (int y = r.y; y < r.y + r.height; y++)
        {
            for (int x = r.x; x < r.x + r.width; x++)
            {
                bool isBorder = (x == r.x || x == r.x + r.width - 1 || y == r.y || y == r.y + r.height - 1);
                px[y * w + x] = isBorder ? ribbonBorder : ribbonBg;
            }
        }

        // 左側のカテゴリカラーピルインレイ(種別の色。攻撃範囲ピルは仕様外なので描かない)
        int pillW = 90;
        for (int y = r.y + 2; y < r.y + r.height - 2; y++)
        {
            for (int x = r.x + 4; x < r.x + 4 + pillW; x++)
            {
                px[y * w + x] = catBadgeCol * 0.85f;
            }
        }
    }

    private static void DrawDescriptionBox(Color[] px, int w, int h, RectInt r, CardTier tier)
    {
        Color boxBg = new Color(0.08f, 0.04f, 0.12f, 0.95f);
        Color boxBorder = new Color(0.42f, 0.32f, 0.52f, 1f);
        Color cornerGold = new Color(0.9f, 0.75f, 0.35f, 1f);

        for (int y = r.y; y < r.y + r.height; y++)
        {
            for (int x = r.x; x < r.x + r.width; x++)
            {
                bool isBorder = (x == r.x || x == r.x + r.width - 1 || y == r.y || y == r.y + r.height - 1);
                px[y * w + x] = isBorder ? boxBorder : boxBg;
            }
        }

        // コーナー装飾金具 ┌ ┐ └ ┘
        int clen = 8;
        // 左下
        for (int i = 0; i < clen; i++) { DrawPixelSafe(px, w, h, r.x + i, r.y, cornerGold); DrawPixelSafe(px, w, h, r.x, r.y + i, cornerGold); }
        // 右下
        for (int i = 0; i < clen; i++) { DrawPixelSafe(px, w, h, r.x + r.width - 1 - i, r.y, cornerGold); DrawPixelSafe(px, w, h, r.x + r.width - 1, r.y + i, cornerGold); }
        // 左上
        for (int i = 0; i < clen; i++) { DrawPixelSafe(px, w, h, r.x + i, r.y + r.height - 1, cornerGold); DrawPixelSafe(px, w, h, r.x, r.y + r.height - 1 - i, cornerGold); }
        // 右上
        for (int i = 0; i < clen; i++) { DrawPixelSafe(px, w, h, r.x + r.width - 1 - i, r.y + r.height - 1, cornerGold); DrawPixelSafe(px, w, h, r.x + r.width - 1, r.y + r.height - 1 - i, cornerGold); }
    }

    private static void DrawHeaderPlate(Color[] px, int w, int h, RectInt r, CardCategory cat, Color cardColor)
    {
        Color plateBorder = new Color(0.85f, 0.72f, 0.45f, 1f);
        Color plateBg = new Color(0.12f, 0.06f, 0.18f, 1f);
        Color catStripe = GetCategoryColor(cat);

        for (int y = r.y; y < r.y + r.height; y++)
        {
            for (int x = r.x; x < r.x + r.width; x++)
            {
                bool isBorder = (x == r.x || x == r.x + r.width - 1 || y == r.y || y == r.y + r.height - 1);
                px[y * w + x] = isBorder ? plateBorder : plateBg;
            }
        }

        // 上部カラーアクセントストライプ
        for (int x = r.x + 2; x < r.x + r.width - 2; x++)
        {
            px[(r.y + r.height - 3) * w + x] = catStripe;
            px[(r.y + r.height - 4) * w + x] = catStripe * 0.8f;
        }
    }

    private static void DrawDifficultyGem(Color[] px, int w, int h, RectInt r, int diff)
    {
        int cx = r.x + r.width / 2;
        int cy = r.y + r.height / 2;
        int rad = r.width / 2 - 2;

        Color gemGold = new Color(1f, 0.88f, 0.35f, 1f);
        Color gemCol = (diff < 0) ? new Color(0.2f, 0.85f, 0.8f, 1f) : // 回復/易化: シアン
                       (diff == 0) ? new Color(0.65f, 0.4f, 0.9f, 1f) : // 標準: アメジスト
                                     new Color(1f, 0.35f, 0.2f, 1f);   // 攻撃/難化: ルビー

        Color gemHi = Color.Lerp(gemCol, Color.white, 0.65f);

        // 八角形ジュエル
        for (int dy = -rad; dy <= rad; dy++)
        {
            for (int dx = -rad; dx <= rad; dx++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= rad + 6 && Mathf.Abs(dx) <= rad && Mathf.Abs(dy) <= rad)
                {
                    bool isBorder = (Mathf.Abs(dx) + Mathf.Abs(dy) >= rad + 4) || (Mathf.Abs(dx) >= rad - 1) || (Mathf.Abs(dy) >= rad - 1);
                    Color c = isBorder ? gemGold : ((dx < -2 && dy > 2) ? gemHi : gemCol);
                    DrawPixelSafe(px, w, h, cx + dx, cy + dy, c);
                }
            }
        }
    }

    private static void DrawPowerMedallion(Color[] px, int w, int h, RectInt r, int power, CardCategory cat)
    {
        Color gold = new Color(0.9f, 0.75f, 0.35f, 1f);
        Color dark = new Color(0.12f, 0.06f, 0.18f, 1f);
        Color red = new Color(0.85f, 0.25f, 0.25f, 1f);

        for (int y = r.y; y < r.y + r.height; y++)
        {
            for (int x = r.x; x < r.x + r.width; x++)
            {
                bool isBorder = (x == r.x || x == r.x + r.width - 1 || y == r.y || y == r.y + r.height - 1);
                px[y * w + x] = isBorder ? gold : dark;
            }
        }

        // 左側のミニ丸アイコン（剣/拳）
        for (int dy = -8; dy <= 8; dy++)
            for (int dx = -8; dx <= 8; dx++)
                if (dx * dx + dy * dy <= 64)
                    DrawPixelSafe(px, w, h, r.x + 16 + dx, r.y + 16 + dy, red);
    }

    private static void DrawLevelBadge(Color[] px, int w, int h, RectInt r, int level, CardTier tier)
    {
        Color gold = new Color(0.9f, 0.75f, 0.35f, 1f);
        Color dark = new Color(0.12f, 0.06f, 0.18f, 1f);

        for (int y = r.y; y < r.y + r.height; y++)
        {
            for (int x = r.x; x < r.x + r.width; x++)
            {
                bool isBorder = (x == r.x || x == r.x + r.width - 1 || y == r.y || y == r.y + r.height - 1);
                px[y * w + x] = isBorder ? gold : dark;
            }
        }
    }

    // =========================================================================
    // カード裏面デザイン
    // =========================================================================

    private static Texture2D CreateCardBackTexture()
    {
        int w = CARD_WIDTH;
        int h = CARD_HEIGHT;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[w * h];
        int cornerR = 12;

        Color gold = new Color(1f, 0.85f, 0.3f, 1f);
        Color goldDark = new Color(0.6f, 0.45f, 0.15f, 1f);
        Color velvetDark = new Color(0.08f, 0.03f, 0.12f, 1f);
        Color velvetMid = new Color(0.18f, 0.07f, 0.28f, 1f);

        int cx = w / 2;
        int cy = h / 2;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (IsOutsideRoundedRect(x, y, 0, 0, w, h, cornerR)) continue;

                // 外枠
                if (IsOnRoundedRectEdge(x, y, 1, 1, w - 2, h - 2, cornerR - 1, 3))
                {
                    px[y * w + x] = gold;
                    continue;
                }

                // 2重目の内金枠
                if (IsOnRoundedRectEdge(x, y, 10, 10, w - 20, h - 20, 6, 2))
                {
                    px[y * w + x] = gold;
                    continue;
                }

                // ダイヤ格子パターン（ハーレクイン・ベルベットカーテン柄）
                bool lattice = ((x + y) % 24 == 0) || ((x - y + 1000) % 24 == 0);
                float distFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float t = Mathf.Clamp01(distFromCenter / (w * 0.7f));
                Color baseCol = Color.Lerp(velvetMid, velvetDark, t);

                if (lattice && x > 12 && x < w - 12 && y > 12 && y < h - 12)
                {
                    baseCol = Color.Lerp(baseCol, goldDark, 0.4f);
                }

                px[y * w + x] = baseCol;
            }
        }

        // 中央の黄金スポットライト円
        int spotRad = 52;
        for (int dy = -spotRad; dy <= spotRad; dy++)
        {
            for (int dx = -spotRad; dx <= spotRad; dx++)
            {
                int distSq = dx * dx + dy * dy;
                if (distSq <= spotRad * spotRad)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x >= 0 && x < w && y >= 0 && y < h)
                    {
                        if (distSq >= (spotRad - 3) * (spotRad - 3))
                        {
                            px[y * w + x] = gold;
                        }
                        else
                        {
                            float t = Mathf.Sqrt(distSq) / spotRad;
                            px[y * w + x] = Color.Lerp(new Color(0.35f, 0.15f, 0.5f, 1f), velvetDark, t);
                        }
                    }
                }
            }
        }

        // 中央の劇場の仮面エンブレム
        DrawTheatricalMaskEmblem(px, w, h, cx, cy);

        // 4隅の装飾リベット
        DrawCornerRivet(px, w, h, 20, h - 20, gold);
        DrawCornerRivet(px, w, h, w - 20, h - 20, gold);
        DrawCornerRivet(px, w, h, 20, 20, gold);
        DrawCornerRivet(px, w, h, w - 20, 20, gold);

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // =========================================================================
    // テキスト合成処理（ピクセルバッファへの直接描画・安全なフォールバック）
    // =========================================================================

    private static void RenderCardTextsToPixels(
        Color[] px,
        int w,
        int h,
        CardInstance card,
        RectInt headerRect,
        RectInt descRect,
        RectInt diffRect,
        RectInt powerRect,
        RectInt levelRect)
    {
        var data = card.Data;

        // 1. 難易度（左上ジュエル内）
        int diff = card.EffectiveDifficulty;
        string diffStr = (diff >= 0) ? $"+{diff}" : $"{diff}";
        DrawStringOnPixels(px, w, h, diffRect.x + 10, diffRect.y + 14, diffStr, Color.white, 3);

        // 2. カード名（ヘッダープレート）
        string nameStr = string.IsNullOrEmpty(data.displayName) ? data.name : data.displayName;
        DrawStringOnPixels(px, w, h, headerRect.x + 10, headerRect.y + 8, nameStr, Color.white, 2);

        // 3. タイプタグ（アート下リボン左）
        string catStr = $"[{GetCategoryNameJp(data.category)}]";
        DrawStringOnPixels(px, w, h, 28, 172, catStr, Color.white, 2);

        // 4. 効果説明（テキストボックス内）
        string descStr = data.description ?? "";
        DrawWrappedStringOnPixels(px, w, h, descRect.x + 12, descRect.y + descRect.height - 20, descRect.width - 24, descStr, new Color(0.95f, 0.95f, 1f, 1f), 2);

        // 【仕様】POW(威力)・レベル・攻撃範囲は表示しない。
    }

    private static void DrawStringOnPixels(Color[] px, int w, int h, int startX, int startY, string text, Color color, int scale)
    {
        if (string.IsNullOrEmpty(text)) return;
        Texture2D textTex = PixelFontRenderer.RenderText(text, color, scale);
        if (textTex == null) return;

        Color[] textPx = textTex.GetPixels();
        int tw = textTex.width;
        int th = textTex.height;

        // パス1: 影（黒・1px右下）を先に描く
        for (int ty = 0; ty < th; ty++)
            for (int tx = 0; tx < tw; tx++)
            {
                float a = textPx[ty * tw + tx].a;
                if (a > 0.05f)
                    DrawPixelSafe(px, w, h, startX + tx + 1, startY + ty - 1, new Color(0f, 0f, 0f, a * 0.7f));
            }
        // パス2: 文字本体を上に描く（影に潰されない）
        for (int ty = 0; ty < th; ty++)
            for (int tx = 0; tx < tw; tx++)
            {
                Color c = textPx[ty * tw + tx];
                if (c.a > 0.05f)
                    DrawPixelSafe(px, w, h, startX + tx, startY + ty, c);
            }
    }

    private static void DrawWrappedStringOnPixels(Color[] px, int w, int h, int startX, int topY, int maxW, string text, Color color, int scale)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 実測幅で maxW に収まるよう折り返す(はみ出さない)
        var lines = WrapText(text, maxW, scale);
        int lineH = LineHeight(scale);

        int curY = topY;
        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line))
                DrawStringOnPixels(px, w, h, startX, curY, line, color, scale);
            curY -= lineH;
            if (curY < 50) break;
        }
    }

    // =========================================================================
    // 補助関数
    // =========================================================================

    private static void DrawPixelSafe(Color[] px, int w, int h, int x, int y, Color c)
    {
        if (x >= 0 && x < w && y >= 0 && y < h)
        {
            if (c.a >= 1f) px[y * w + x] = c;
            else px[y * w + x] = Color.Lerp(px[y * w + x], c, c.a);
        }
    }

    private static bool IsOutsideRoundedRect(int x, int y, int rx, int ry, int rw, int rh, int r)
    {
        if (x < rx || x >= rx + rw || y < ry || y >= ry + rh) return true;

        // 4隅の判定
        if (x < rx + r && y < ry + r) return Vector2.Distance(new Vector2(x, y), new Vector2(rx + r, ry + r)) > r;
        if (x >= rx + rw - r && y < ry + r) return Vector2.Distance(new Vector2(x, y), new Vector2(rx + rw - r - 1, ry + r)) > r;
        if (x < rx + r && y >= ry + rh - r) return Vector2.Distance(new Vector2(x, y), new Vector2(rx + r, ry + rh - r - 1)) > r;
        if (x >= rx + rw - r && y >= ry + rh - r) return Vector2.Distance(new Vector2(x, y), new Vector2(rx + rw - r - 1, ry + rh - r - 1)) > r;

        return false;
    }

    private static bool IsOnRoundedRectEdge(int x, int y, int rx, int ry, int rw, int rh, int r, int thickness)
    {
        if (IsOutsideRoundedRect(x, y, rx, ry, rw, rh, r)) return false;
        return IsOutsideRoundedRect(x, y, rx + thickness, ry + thickness, rw - thickness * 2, rh - thickness * 2, Mathf.Max(0, r - thickness));
    }

    private static void BlitSpriteToPixels(Color[] px, int w, int h, Sprite spr, int cx, int cy, int targetSize)
    {
        Texture2D srcTex = spr.texture;
        if (srcTex == null) return;

        Rect sprRect = spr.rect;
        int half = targetSize / 2;

        for (int dy = -half; dy < half; dy++)
        {
            for (int dx = -half; dx < half; dx++)
            {
                float u = sprRect.x + ((dx + half) / (float)targetSize) * sprRect.width;
                float v = sprRect.y + ((dy + half) / (float)targetSize) * sprRect.height;
                Color srcCol = srcTex.GetPixel((int)u, (int)v);
                if (srcCol.a > 0.1f)
                {
                    DrawPixelSafe(px, w, h, cx + dx, cy + dy, srcCol);
                }
            }
        }
    }

    public static Color GetCategoryColor(CardCategory cat)
    {
        switch (cat)
        {
            case CardCategory.Attack:  return new Color(0.95f, 0.30f, 0.30f, 1f); // レッド
            case CardCategory.Defend:  return new Color(0.35f, 0.65f, 1.00f, 1f); // ブルー
            case CardCategory.Heal:    return new Color(0.30f, 0.90f, 0.50f, 1f); // グリーン
            case CardCategory.Buff:    return new Color(1.00f, 0.80f, 0.20f, 1f); // イエロー
            case CardCategory.Special: return new Color(0.75f, 0.40f, 0.95f, 1f); // パープル
            default:                   return Color.white;
        }
    }

    public static string GetCategoryNameJp(CardCategory cat)
    {
        switch (cat)
        {
            case CardCategory.Attack:  return "アタック";
            case CardCategory.Defend:  return "ガード";
            case CardCategory.Heal:    return "ヒール";
            case CardCategory.Buff:    return "バフ";
            case CardCategory.Special: return "特殊";
            default:                   return "通常";
        }
    }

    public static string GetRangeNameJp(CardAttackRange range)
    {
        switch (range)
        {
            case CardAttackRange.Single: return "単体";
            case CardAttackRange.Upper:  return "上半身";
            case CardAttackRange.Lower:  return "下半身";
            case CardAttackRange.All:    return "全体";
            default:                     return "単体";
        }
    }
}
