using UnityEngine;

/// <summary>
/// 弾幕フェーズ中のドット風フレームを描画。
/// 中央のプレイフィールドを囲む8bit風の枠線。
/// </summary>
public class DanmakuFrameView : MonoBehaviour
{
    [Header("フレーム設定")]
    [Tooltip("フレームの幅(px)")]
    public float frameWidth = 800f;
    [Tooltip("フレームの高さ(px)")]
    public float frameHeight = 600f;
    [Tooltip("枠線の太さ(px)")]
    public float borderThickness = 8f;
    [Tooltip("コーナー飾りのサイズ")]
    public float cornerSize = 24f;

    [Header("アニメーション")]
    [Tooltip("枠線の点滅速度")]
    public float pulseSpeed = 2f;

    private bool isVisible = false;
    private float pulseTimer;

    // テクスチャ
    private Texture2D borderTexture;
    private Texture2D cornerTexture;
    private Texture2D innerBorderTexture;
    private Texture2D accentDotTexture;

    public void SetVisible(bool visible)
    {
        isVisible = visible;
    }

    void Start()
    {
        CreateTextures();
    }

    void OnDestroy()
    {
        DestroyTextures();
    }

    private void CreateTextures()
    {
        // 外枠(紫)
        borderTexture = CreateSolidTexture(GameColorScheme.FrameOuter);

        // 内枠(暗紫)
        innerBorderTexture = CreateSolidTexture(GameColorScheme.FrameInner);

        // コーナー飾り(黄色アクセント付き)
        cornerTexture = CreateCornerTexture();

        // アクセントドット
        accentDotTexture = CreateSolidTexture(GameColorScheme.AccentBright);
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

    private Texture2D CreateCornerTexture()
    {
        int size = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[size * size];
        Color outer = GameColorScheme.FrameOuter;
        Color accent = GameColorScheme.AccentBright;
        Color clear = new Color(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // L字型コーナー
                if (x < 3 || y < 3)
                {
                    // 外側は紫
                    if (x == 0 || y == 0)
                    {
                        pixels[y * size + x] = outer;
                    }
                    // 内側は黄色アクセント
                    else if (x == 1 || y == 1)
                    {
                        pixels[y * size + x] = accent;
                    }
                    else
                    {
                        pixels[y * size + x] = outer;
                    }
                }
                else
                {
                    pixels[y * size + x] = clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void DestroyTextures()
    {
        if (borderTexture != null) Destroy(borderTexture);
        if (cornerTexture != null) Destroy(cornerTexture);
        if (innerBorderTexture != null) Destroy(innerBorderTexture);
        if (accentDotTexture != null) Destroy(accentDotTexture);
    }

    void Update()
    {
        pulseTimer += Time.deltaTime * pulseSpeed;
    }

    void OnGUI()
    {
        if (!isVisible) return;

        float x = (Screen.width - frameWidth) / 2f;
        float y = (Screen.height - frameHeight) / 2f;

        // 点滅エフェクト
        float pulse = 0.7f + 0.3f * Mathf.Sin(pulseTimer * Mathf.PI);
        Color pulseColor = new Color(pulse, pulse, pulse, 1f);
        GUI.color = pulseColor;

        // 外枠(上下左右)
        // 上
        GUI.DrawTexture(new Rect(x, y, frameWidth, borderThickness), borderTexture);
        // 下
        GUI.DrawTexture(new Rect(x, y + frameHeight - borderThickness, frameWidth, borderThickness), borderTexture);
        // 左
        GUI.DrawTexture(new Rect(x, y, borderThickness, frameHeight), borderTexture);
        // 右
        GUI.DrawTexture(new Rect(x + frameWidth - borderThickness, y, borderThickness, frameHeight), borderTexture);

        // 内枠(少し内側に)
        float innerOffset = borderThickness + 2f;
        float innerThickness = 2f;
        // 上
        GUI.DrawTexture(new Rect(x + innerOffset, y + innerOffset, frameWidth - innerOffset * 2, innerThickness), innerBorderTexture);
        // 下
        GUI.DrawTexture(new Rect(x + innerOffset, y + frameHeight - innerOffset - innerThickness, frameWidth - innerOffset * 2, innerThickness), innerBorderTexture);
        // 左
        GUI.DrawTexture(new Rect(x + innerOffset, y + innerOffset, innerThickness, frameHeight - innerOffset * 2), innerBorderTexture);
        // 右
        GUI.DrawTexture(new Rect(x + frameWidth - innerOffset - innerThickness, y + innerOffset, innerThickness, frameHeight - innerOffset * 2), innerBorderTexture);

        GUI.color = Color.white;

        // コーナー飾り(四隅)
        DrawCorners(x, y);

        // アクセントドット(枠上に点々)
        DrawAccentDots(x, y);
    }

    private void DrawCorners(float x, float y)
    {
        float cs = cornerSize;

        // 左上
        GUI.DrawTexture(new Rect(x - 4f, y - 4f, cs, cs), cornerTexture);

        // 右上(反転)
        var matrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), new Vector2(x + frameWidth + 4f, y + cs / 2f));
        GUI.DrawTexture(new Rect(x + frameWidth + 4f - cs, y - 4f, cs, cs), cornerTexture);
        GUI.matrix = matrix;

        // 左下(反転)
        GUIUtility.ScaleAroundPivot(new Vector2(1f, -1f), new Vector2(x + cs / 2f, y + frameHeight + 4f));
        GUI.DrawTexture(new Rect(x - 4f, y + frameHeight + 4f - cs, cs, cs), cornerTexture);
        GUI.matrix = matrix;

        // 右下(両反転)
        GUIUtility.ScaleAroundPivot(new Vector2(-1f, -1f), new Vector2(x + frameWidth + 4f, y + frameHeight + 4f));
        GUI.DrawTexture(new Rect(x + frameWidth + 4f - cs, y + frameHeight + 4f - cs, cs, cs), cornerTexture);
        GUI.matrix = matrix;
    }

    private void DrawAccentDots(float x, float y)
    {
        float dotSize = 4f;
        float spacing = 32f;
        int animOffset = (int)(pulseTimer * 8f) % 2;

        // 上辺
        for (float dx = spacing; dx < frameWidth - spacing; dx += spacing)
        {
            if (((int)(dx / spacing) + animOffset) % 2 == 0)
            {
                GUI.DrawTexture(new Rect(x + dx - dotSize / 2f, y - dotSize - 2f, dotSize, dotSize), accentDotTexture);
            }
        }

        // 下辺
        for (float dx = spacing; dx < frameWidth - spacing; dx += spacing)
        {
            if (((int)(dx / spacing) + animOffset + 1) % 2 == 0)
            {
                GUI.DrawTexture(new Rect(x + dx - dotSize / 2f, y + frameHeight + 2f, dotSize, dotSize), accentDotTexture);
            }
        }

        // 左辺
        for (float dy = spacing; dy < frameHeight - spacing; dy += spacing)
        {
            if (((int)(dy / spacing) + animOffset) % 2 == 0)
            {
                GUI.DrawTexture(new Rect(x - dotSize - 2f, y + dy - dotSize / 2f, dotSize, dotSize), accentDotTexture);
            }
        }

        // 右辺
        for (float dy = spacing; dy < frameHeight - spacing; dy += spacing)
        {
            if (((int)(dy / spacing) + animOffset + 1) % 2 == 0)
            {
                GUI.DrawTexture(new Rect(x + frameWidth + 2f, y + dy - dotSize / 2f, dotSize, dotSize), accentDotTexture);
            }
        }
    }
}
