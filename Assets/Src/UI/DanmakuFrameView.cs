using UnityEngine;

/// <summary>
/// 弾幕フェーズ中のドット風フレームを描画。
/// 中央のプレイフィールドを囲む8bit風の枠線(外枠＋内枠のみ。装飾・点滅なし)。
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

    private bool isVisible = false;

    // 実プレイフィールド(ワールド)。設定されていれば、これをカメラ投影して枠位置・サイズを合わせる。
    private SpriteRenderer fieldSr;

    /// <summary>枠を合わせる対象フィールドの SpriteRenderer を設定。null なら従来の固定サイズ表示。</summary>
    public void SetField(SpriteRenderer field)
    {
        fieldSr = field;
    }

    // テクスチャ
    private Texture2D borderTexture;

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
        // 外枠(紫)のみ。内枠・装飾は描かない。
        borderTexture = CreateSolidTexture(GameColorScheme.FrameOuter);
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

    private void DestroyTextures()
    {
        if (borderTexture != null) Destroy(borderTexture);
    }

    void OnGUI()
    {
        if (!isVisible) return;

        float x, y;
        Camera cam = Camera.main;
        if (fieldSr != null && cam != null)
        {
            // フィールドのワールド境界をスクリーンへ投影して枠の位置・サイズを算出
            Bounds b = fieldSr.bounds;
            Vector3 s0 = cam.WorldToScreenPoint(new Vector3(b.min.x, b.min.y, 0f));
            Vector3 s1 = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y, 0f));
            float xMin = Mathf.Min(s0.x, s1.x);
            float xMax = Mathf.Max(s0.x, s1.x);
            float yMinS = Mathf.Min(s0.y, s1.y);
            float yMaxS = Mathf.Max(s0.y, s1.y);
            frameWidth = xMax - xMin;
            frameHeight = yMaxS - yMinS;
            x = xMin;
            y = Screen.height - yMaxS;   // GUI は上原点・下向きなので反転
        }
        else
        {
            x = (Screen.width - frameWidth) / 2f;
            y = (Screen.height - frameHeight) / 2f;
        }

        // 外枠(上下左右) — 点滅なしの静的表示
        GUI.color = Color.white;
        // 上
        GUI.DrawTexture(new Rect(x, y, frameWidth, borderThickness), borderTexture);
        // 下
        GUI.DrawTexture(new Rect(x, y + frameHeight - borderThickness, frameWidth, borderThickness), borderTexture);
        // 左
        GUI.DrawTexture(new Rect(x, y, borderThickness, frameHeight), borderTexture);
        // 右
        GUI.DrawTexture(new Rect(x + frameWidth - borderThickness, y, borderThickness, frameHeight), borderTexture);
    }
}
