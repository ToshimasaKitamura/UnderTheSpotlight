using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 8bit風ピクセルフォントをテクスチャで描画するユーティリティ。
/// IMGUIで使用可能なTexture2Dを生成します。
/// 日本語は美咲フォント(8x8ピクセル)を使用。
/// </summary>
public static class PixelFontRenderer
{
    // 基本の5x7ピクセルフォント定義
    private static Dictionary<char, byte[]> fontData;
    private static Dictionary<string, Texture2D> textCache = new Dictionary<string, Texture2D>();

    // 美咲フォント(日本語用)
    private static Font misakiFont;
    private static bool misakiFontLoaded = false;

    // ピクセルサイズ（スケール）- 大きめに設定
    public const int PIXEL_SCALE = 5;
    public const int CHAR_WIDTH = 5;
    public const int CHAR_HEIGHT = 7;
    public const int JP_CHAR_WIDTH = 8;
    public const int JP_CHAR_HEIGHT = 8;
    public const int CHAR_SPACING = 1;

    static PixelFontRenderer()
    {
        InitializeFontData();
        LoadMisakiFont();
    }

    /// <summary>
    /// 美咲フォントをロード
    /// </summary>
    private static void LoadMisakiFont()
    {
        misakiFont = Resources.Load<Font>("Fonts/misaki_gothic");
        if (misakiFont == null)
        {
            // Resources以外からもロードを試みる
            misakiFont = Resources.Load<Font>("misaki_gothic");
        }
        misakiFontLoaded = (misakiFont != null);
        if (!misakiFontLoaded)
        {
            Debug.LogWarning("美咲フォントが見つかりません。Assets/Resources/Fonts/misaki_gothic.ttf を配置してください。");
        }
    }

    private static void InitializeFontData()
    {
        fontData = new Dictionary<char, byte[]>();

        // 数字 0-9
        fontData['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
        fontData['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
        fontData['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F };
        fontData['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E };
        fontData['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
        fontData['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
        fontData['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
        fontData['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
        fontData['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
        fontData['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };

        // アルファベット大文字 A-Z
        fontData['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11 };
        fontData['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E };
        fontData['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
        fontData['D'] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C };
        fontData['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
        fontData['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 };
        fontData['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F };
        fontData['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
        fontData['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
        fontData['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C };
        fontData['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 };
        fontData['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F };
        fontData['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 };
        fontData['N'] = new byte[] { 0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11 };
        fontData['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
        fontData['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
        fontData['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D };
        fontData['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
        fontData['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E };
        fontData['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
        fontData['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
        fontData['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 };
        fontData['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 };
        fontData['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 };
        fontData['Y'] = new byte[] { 0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04 };
        fontData['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F };

        // 小文字 a-z
        fontData['a'] = new byte[] { 0x00, 0x00, 0x0E, 0x01, 0x0F, 0x11, 0x0F };
        fontData['b'] = new byte[] { 0x10, 0x10, 0x16, 0x19, 0x11, 0x11, 0x1E };
        fontData['c'] = new byte[] { 0x00, 0x00, 0x0E, 0x10, 0x10, 0x11, 0x0E };
        fontData['d'] = new byte[] { 0x01, 0x01, 0x0D, 0x13, 0x11, 0x11, 0x0F };
        fontData['e'] = new byte[] { 0x00, 0x00, 0x0E, 0x11, 0x1F, 0x10, 0x0E };
        fontData['f'] = new byte[] { 0x06, 0x09, 0x08, 0x1C, 0x08, 0x08, 0x08 };
        fontData['g'] = new byte[] { 0x00, 0x0F, 0x11, 0x11, 0x0F, 0x01, 0x0E };
        fontData['h'] = new byte[] { 0x10, 0x10, 0x16, 0x19, 0x11, 0x11, 0x11 };
        fontData['i'] = new byte[] { 0x04, 0x00, 0x0C, 0x04, 0x04, 0x04, 0x0E };
        fontData['j'] = new byte[] { 0x02, 0x00, 0x06, 0x02, 0x02, 0x12, 0x0C };
        fontData['k'] = new byte[] { 0x10, 0x10, 0x12, 0x14, 0x18, 0x14, 0x12 };
        fontData['l'] = new byte[] { 0x0C, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
        fontData['m'] = new byte[] { 0x00, 0x00, 0x1A, 0x15, 0x15, 0x11, 0x11 };
        fontData['n'] = new byte[] { 0x00, 0x00, 0x16, 0x19, 0x11, 0x11, 0x11 };
        fontData['o'] = new byte[] { 0x00, 0x00, 0x0E, 0x11, 0x11, 0x11, 0x0E };
        fontData['p'] = new byte[] { 0x00, 0x00, 0x1E, 0x11, 0x1E, 0x10, 0x10 };
        fontData['q'] = new byte[] { 0x00, 0x00, 0x0D, 0x13, 0x0F, 0x01, 0x01 };
        fontData['r'] = new byte[] { 0x00, 0x00, 0x16, 0x19, 0x10, 0x10, 0x10 };
        fontData['s'] = new byte[] { 0x00, 0x00, 0x0E, 0x10, 0x0E, 0x01, 0x1E };
        fontData['t'] = new byte[] { 0x08, 0x08, 0x1C, 0x08, 0x08, 0x09, 0x06 };
        fontData['u'] = new byte[] { 0x00, 0x00, 0x11, 0x11, 0x11, 0x13, 0x0D };
        fontData['v'] = new byte[] { 0x00, 0x00, 0x11, 0x11, 0x11, 0x0A, 0x04 };
        fontData['w'] = new byte[] { 0x00, 0x00, 0x11, 0x11, 0x15, 0x15, 0x0A };
        fontData['x'] = new byte[] { 0x00, 0x00, 0x11, 0x0A, 0x04, 0x0A, 0x11 };
        fontData['y'] = new byte[] { 0x00, 0x00, 0x11, 0x11, 0x0F, 0x01, 0x0E };
        fontData['z'] = new byte[] { 0x00, 0x00, 0x1F, 0x02, 0x04, 0x08, 0x1F };

        // 記号
        fontData[' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        fontData['!'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x00, 0x00, 0x04 };
        fontData['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04 };
        fontData[','] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x08 };
        fontData[':'] = new byte[] { 0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00 };
        fontData['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
        fontData['+'] = new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 };
        fontData['*'] = new byte[] { 0x00, 0x04, 0x15, 0x0E, 0x15, 0x04, 0x00 };
        fontData['/'] = new byte[] { 0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x00 };
        fontData['('] = new byte[] { 0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02 };
        fontData[')'] = new byte[] { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 };
        fontData['['] = new byte[] { 0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E };
        fontData[']'] = new byte[] { 0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E };
        fontData['<'] = new byte[] { 0x02, 0x04, 0x08, 0x10, 0x08, 0x04, 0x02 };
        fontData['>'] = new byte[] { 0x08, 0x04, 0x02, 0x01, 0x02, 0x04, 0x08 };
        fontData['?'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04 };
        fontData['_'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F };
        fontData['#'] = new byte[] { 0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A };
        fontData['@'] = new byte[] { 0x0E, 0x11, 0x17, 0x15, 0x17, 0x10, 0x0E };
        fontData['%'] = new byte[] { 0x18, 0x19, 0x02, 0x04, 0x08, 0x13, 0x03 };
        fontData['&'] = new byte[] { 0x0C, 0x12, 0x14, 0x08, 0x15, 0x12, 0x0D };
        fontData['='] = new byte[] { 0x00, 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x00 };
        fontData['"'] = new byte[] { 0x0A, 0x0A, 0x0A, 0x00, 0x00, 0x00, 0x00 };
        fontData['\''] = new byte[] { 0x04, 0x04, 0x04, 0x00, 0x00, 0x00, 0x00 };

        // 矢印
        fontData['\u2190'] = new byte[] { 0x00, 0x04, 0x08, 0x1F, 0x08, 0x04, 0x00 }; // ←
        fontData['\u2191'] = new byte[] { 0x04, 0x0E, 0x15, 0x04, 0x04, 0x04, 0x04 }; // ↑
        fontData['\u2192'] = new byte[] { 0x00, 0x04, 0x02, 0x1F, 0x02, 0x04, 0x00 }; // →
        fontData['\u2193'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x15, 0x0E, 0x04 }; // ↓

        // ハート、スター
        fontData['\u2665'] = new byte[] { 0x00, 0x0A, 0x1F, 0x1F, 0x0E, 0x04, 0x00 }; // ♥
        fontData['\u2605'] = new byte[] { 0x04, 0x04, 0x1F, 0x0E, 0x0A, 0x11, 0x00 }; // ★
    }

    /// <summary>
    /// 文字が日本語かどうか判定
    /// </summary>
    private static bool IsJapanese(char c)
    {
        // ひらがな、カタカナ、CJK統合漢字、全角記号をチェック
        return (c >= '\u3040' && c <= '\u309F') ||  // ひらがな
               (c >= '\u30A0' && c <= '\u30FF') ||  // カタカナ
               (c >= '\u4E00' && c <= '\u9FAF') ||  // CJK統合漢字
               (c >= '\u3000' && c <= '\u303F') ||  // CJK記号・句読点
               (c >= '\uFF00' && c <= '\uFFEF');    // 全角ASCII、半角カナ
    }

    /// <summary>
    /// テキストをピクセルフォントのTexture2Dとして描画
    /// </summary>
    public static Texture2D RenderText(string text, Color color, int scale = PIXEL_SCALE)
    {
        string cacheKey = $"{text}_{ColorToHex(color)}_{scale}";
        if (textCache.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
        {
            return cached;
        }

        // 日本語が含まれる場合は美咲フォントを使う
        bool hasJapanese = false;
        foreach (char c in text)
        {
            if (IsJapanese(c))
            {
                hasJapanese = true;
                break;
            }
        }

        Texture2D texture;
        if (hasJapanese && misakiFontLoaded)
        {
            // OnGUI内でのみ美咲フォントを使用（GLコマンドが必要なため）
            // OnGUI外ではビットマップフォントにフォールバック
            if (Event.current != null)
            {
                texture = RenderTextWithMisakiFont(text, color, scale);
            }
            else
            {
                // OnGUI外の場合はビットマップフォントを使用
                texture = RenderTextBitmap(text, color, scale);
            }
        }
        else
        {
            texture = RenderTextBitmap(text, color, scale);
        }

        // キャッシュに追加（最大200エントリ）
        if (textCache.Count > 200)
        {
            ClearCache();
        }
        textCache[cacheKey] = texture;

        return texture;
    }

    /// <summary>
    /// 美咲フォントを使ってテキストを描画
    /// OnGUI内で呼ばれた場合はGUIスタイルを使用、それ以外はペンディングリストに追加
    /// </summary>
    private static Texture2D RenderTextWithMisakiFont(string text, Color color, int scale)
    {
        // 美咲フォントは8ポイント基準、スケールを掛ける
        int fontSize = 8 * scale;

        // GUIStyleでサイズを計算
        GUIStyle style = new GUIStyle();
        style.font = misakiFont;
        style.fontSize = fontSize;
        style.normal.textColor = Color.white; // 白で描画して後で色を変える

        Vector2 size = style.CalcSize(new GUIContent(text));
        int width = Mathf.Max(1, Mathf.CeilToInt(size.x) + 4);
        int height = Mathf.Max(1, Mathf.CeilToInt(size.y) + 4);

        // RenderTextureに描画
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);

        // GL.PushMatrix/PopMatrixで描画
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, width, 0, height);

        // テキストを描画するためのマテリアル
        if (misakiFont.material != null)
        {
            misakiFont.material.SetPass(0);
        }

        // 文字を一つずつ描画
        misakiFont.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);

        float xPos = 2;
        float yPos = 2;
        GL.Begin(GL.QUADS);
        GL.Color(Color.white);

        foreach (char c in text)
        {
            if (misakiFont.GetCharacterInfo(c, out CharacterInfo info, fontSize, FontStyle.Normal))
            {
                // 文字の四隅
                float x0 = xPos + info.minX;
                float y0 = yPos + info.maxY;
                float x1 = xPos + info.maxX;
                float y1 = yPos + info.minY;

                // UV座標
                Vector2 uv0 = info.uvBottomLeft;
                Vector2 uv1 = info.uvBottomRight;
                Vector2 uv2 = info.uvTopRight;
                Vector2 uv3 = info.uvTopLeft;

                GL.TexCoord2(uv0.x, uv0.y); GL.Vertex3(x0, y1, 0);
                GL.TexCoord2(uv1.x, uv1.y); GL.Vertex3(x1, y1, 0);
                GL.TexCoord2(uv2.x, uv2.y); GL.Vertex3(x1, y0, 0);
                GL.TexCoord2(uv3.x, uv3.y); GL.Vertex3(x0, y0, 0);

                xPos += info.advance;
            }
        }
        GL.End();
        GL.PopMatrix();

        // RenderTextureからTexture2Dにコピー
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        // 白いピクセルを指定色に変換
        Color[] pixels = texture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 0.1f)
            {
                pixels[i] = new Color(color.r, color.g, color.b, pixels[i].a * color.a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    /// <summary>
    /// ビットマップフォントで描画（フォールバック用）
    /// </summary>
    private static Texture2D RenderTextBitmap(string text, Color color, int scale)
    {
        // 文字幅を計算（日本語は8px、英数字は5px）
        int totalWidth = 0;
        int maxHeight = 0;
        foreach (char c in text)
        {
            if (IsJapanese(c))
            {
                totalWidth += (JP_CHAR_WIDTH + CHAR_SPACING) * scale;
                maxHeight = Mathf.Max(maxHeight, JP_CHAR_HEIGHT * scale);
            }
            else
            {
                totalWidth += (CHAR_WIDTH + CHAR_SPACING) * scale;
                maxHeight = Mathf.Max(maxHeight, CHAR_HEIGHT * scale);
            }
        }

        if (totalWidth <= 0) totalWidth = 1;
        if (maxHeight <= 0) maxHeight = JP_CHAR_HEIGHT * scale;

        Texture2D texture = new Texture2D(totalWidth, maxHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        // 透明で初期化
        Color[] pixels = new Color[totalWidth * maxHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        int xOffset = 0;
        foreach (char c in text)
        {
            if (IsJapanese(c))
            {
                // 日本語文字は「?」で表示（フォントがない場合）
                if (fontData.TryGetValue('?', out byte[] qData))
                {
                    int yOffset = (maxHeight - CHAR_HEIGHT * scale) / 2;
                    DrawCharWithOffset(pixels, totalWidth, maxHeight, xOffset, yOffset, qData, color, scale);
                }
                xOffset += (JP_CHAR_WIDTH + CHAR_SPACING) * scale;
            }
            else
            {
                // 英数字・記号
                char upperC = char.ToUpper(c);
                if (fontData.TryGetValue(upperC, out byte[] charData))
                {
                    int yOffset = (maxHeight - CHAR_HEIGHT * scale) / 2;
                    DrawCharWithOffset(pixels, totalWidth, maxHeight, xOffset, yOffset, charData, color, scale);
                }
                else if (fontData.TryGetValue(c, out byte[] charDataLower))
                {
                    int yOffset = (maxHeight - CHAR_HEIGHT * scale) / 2;
                    DrawCharWithOffset(pixels, totalWidth, maxHeight, xOffset, yOffset, charDataLower, color, scale);
                }
                xOffset += (CHAR_WIDTH + CHAR_SPACING) * scale;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static void DrawCharWithOffset(Color[] pixels, int texWidth, int texHeight, int xOffset, int yOffset, byte[] charData, Color color, int scale)
    {
        for (int row = 0; row < CHAR_HEIGHT; row++)
        {
            byte rowData = charData[row];
            for (int col = 0; col < CHAR_WIDTH; col++)
            {
                bool isPixelSet = ((rowData >> (CHAR_WIDTH - 1 - col)) & 1) == 1;
                if (isPixelSet)
                {
                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int px = xOffset + col * scale + sx;
                            int py = texHeight - 1 - (row * scale + sy + yOffset / scale);
                            if (px >= 0 && px < texWidth && py >= 0 && py < texHeight)
                            {
                                pixels[py * texWidth + px] = color;
                            }
                        }
                    }
                }
            }
        }
    }

    private static void DrawChar(Color[] pixels, int texWidth, int texHeight, int xOffset, byte[] charData, Color color, int scale)
    {
        for (int row = 0; row < CHAR_HEIGHT; row++)
        {
            byte rowData = charData[row];
            for (int col = 0; col < CHAR_WIDTH; col++)
            {
                bool isPixelSet = ((rowData >> (CHAR_WIDTH - 1 - col)) & 1) == 1;
                if (isPixelSet)
                {
                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int px = xOffset + col * scale + sx;
                            int py = texHeight - 1 - (row * scale + sy);
                            if (px >= 0 && px < texWidth && py >= 0 && py < texHeight)
                            {
                                pixels[py * texWidth + px] = color;
                            }
                        }
                    }
                }
            }
        }
    }

    private static string ColorToHex(Color c)
    {
        return $"{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}{(int)(c.a * 255):X2}";
    }

    /// <summary>
    /// ドット絵風の四角形を描画するTexture2Dを生成
    /// </summary>
    public static Texture2D CreatePixelBox(int width, int height, Color fillColor, Color borderColor, int borderWidth = 2)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < borderWidth || x >= width - borderWidth ||
                                y < borderWidth || y >= height - borderWidth;
                pixels[y * width + x] = isBorder ? borderColor : fillColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// 8bit風のグラデーション背景を生成
    /// </summary>
    public static Texture2D CreateDitheredGradient(int width, int height, Color topColor, Color bottomColor, int ditherScale = 4)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float t = (float)y / height;
            int step = Mathf.FloorToInt(t * 8);
            float stepT = step / 8f;

            for (int x = 0; x < width; x++)
            {
                bool dither = ((x / ditherScale) + (y / ditherScale)) % 2 == 0;
                float localT = dither ? stepT : Mathf.Min(1f, stepT + 0.125f);
                pixels[y * width + x] = Color.Lerp(bottomColor, topColor, localT);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// スターのドット絵を生成
    /// </summary>
    public static Texture2D CreatePixelStar(int size, Color color)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        int[] starPattern = {
            0b00010000,
            0b00010000,
            0b00111000,
            0b11111110,
            0b00111000,
            0b01010100,
            0b10010010,
            0b00010000,
        };

        int scale = size / 8;
        if (scale < 1) scale = 1;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                bool isSet = ((starPattern[row] >> (7 - col)) & 1) == 1;
                if (isSet)
                {
                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int px = col * scale + sx;
                            int py = size - 1 - (row * scale + sy);
                            if (px < size && py >= 0 && py < size)
                            {
                                pixels[py * size + px] = color;
                            }
                        }
                    }
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// キャッシュをクリア
    /// </summary>
    public static void ClearCache()
    {
        foreach (var tex in textCache.Values)
        {
            if (tex != null) Object.Destroy(tex);
        }
        textCache.Clear();
    }
}
