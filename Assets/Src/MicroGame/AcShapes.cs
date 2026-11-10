using UnityEngine;

/// <summary>
/// アクションコマンド用の図形スプライト(丸・リング・三角)を実行時に生成してキャッシュする。
/// 外部画像に依存せず、どの解像度でも滑らかに見えるようアンチエイリアス付きで描く。
/// </summary>
public static class AcShapes
{
    private const int Size = 128;

    private static Sprite circle;
    private static Sprite ring;
    private static Sprite triangle;
    private static Sprite square;

    /// <summary>白い四角(Image.Type.Filled でゲージを作るときの素材)。</summary>
    public static Sprite Square()
    {
        if (square == null) square = Make("AcSquare", (x, y) => 1f);
        return square;
    }

    /// <summary>塗りつぶしの丸。</summary>
    public static Sprite Circle()
    {
        if (circle == null) circle = Make("AcCircle", CircleAlpha);
        return circle;
    }

    /// <summary>リング(太さは半径の約 9%)。</summary>
    public static Sprite Ring()
    {
        if (ring == null) ring = Make("AcRing", RingAlpha);
        return ring;
    }

    /// <summary>上向きの二等辺三角形(底辺=幅いっぱい、頂点=上端中央)。回転させて矢印にも使う。</summary>
    public static Sprite Triangle()
    {
        if (triangle == null) triangle = Make("AcTriangle", TriangleAlpha);
        return triangle;
    }

    private delegate float AlphaFunc(float x, float y);

    private static Sprite Make(string name, AlphaFunc alpha)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                px[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        return s;
    }

    // 画素中心 (x,y) に対するアルファ。境界から 0.5px でなめらかに落とす。
    private static float CircleAlpha(float x, float y)
    {
        float r = Size * 0.5f - 1f;
        float d = Dist(x, y, Size * 0.5f, Size * 0.5f);
        return r - d + 0.5f;
    }

    private static float RingAlpha(float x, float y)
    {
        float r = Size * 0.5f - 1f;
        float t = r * 0.09f;
        float d = Dist(x, y, Size * 0.5f, Size * 0.5f);
        return Mathf.Min(r - d, d - (r - t)) + 0.5f;
    }

    private static float TriangleAlpha(float x, float y)
    {
        // 頂点(反時計回り): 左下 → 右下 → 上
        float m = 1f;
        Vector2 a = new Vector2(m, m);
        Vector2 b = new Vector2(Size - m, m);
        Vector2 c = new Vector2(Size * 0.5f, Size - m);
        Vector2 p = new Vector2(x, y);
        float d = Mathf.Min(EdgeDist(p, a, b), Mathf.Min(EdgeDist(p, b, c), EdgeDist(p, c, a)));
        return d + 0.5f;
    }

    // 反時計回りの辺 a→b に対し、内側を正とする符号付き距離。
    private static float EdgeDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 e = b - a;
        float len = Mathf.Sqrt(e.x * e.x + e.y * e.y);
        if (len <= 0f) return 0f;
        return ((p.y - a.y) * e.x - (p.x - a.x) * e.y) / len;
    }

    private static float Dist(float x, float y, float cx, float cy)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
