using UnityEngine;

/// <summary>
/// ゲーム共通のマウスカーソル絵(矢印ポインタ)を生成する静的ユーティリティ。
/// カード選択・キャラ選択など各所で同じカーソルを使うため、実装はここに一本化する。
/// 先端は左上で、pivot は <see cref="TipPivot"/>。
/// </summary>
public static class CursorSprite
{
    /// <summary>カーソル先端(左上)の pivot。RectTransform.pivot にこれを使うと先端がその座標に来る。</summary>
    public static readonly Vector2 TipPivot = new Vector2(0.125f, 0.9375f);

    /// <summary>定番の矢印マウスポインタ(指定色＋暗いアウトライン)。先端が左上。</summary>
    public static Sprite CreatePointer(Color color)
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color fillCol = color;
        Color outline = new Color(0.08f, 0.06f, 0.12f, 1f);

        // 矢印ポインタ形状(y-down のグリッド単位, 先端が原点)
        Vector2[] grid =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 17f),
            new Vector2(4.6f, 12.8f),
            new Vector2(7.6f, 18.5f),
            new Vector2(10.1f, 17.3f),
            new Vector2(6.7f, 11.8f),
            new Vector2(12f, 11.5f),
        };
        const float scale = 3.2f;
        const float offX = 8f;
        const float apexY = 60f;   // 先端の y(テクスチャ y-up)

        var poly = new Vector2[grid.Length];
        for (int k = 0; k < grid.Length; k++)
            poly[k] = new Vector2(offX + grid[k].x * scale, apexY - grid[k].y * scale);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), poly))
                    px[y * size + x] = fillCol;

        // 1px の暗いアウトラインを外側に付ける
        var outlined = (Color[])px.Clone();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (px[idx].a > 0f) continue;
                bool near = false;
                for (int oy = -1; oy <= 1 && !near; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                        if (px[ny * size + nx].a > 0f) { near = true; break; }
                    }
                if (near) outlined[idx] = outline;
            }

        tex.SetPixels(outlined);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), TipPivot, size);
    }

    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
