using UnityEngine;

/// <summary>
/// 各プレイヤーのボム残数を表示する UI（8bit ピクセル風）。
/// OnGUI ベースで描画し、PixelFontRenderer を使用。
/// BombController.OnChanged を購読して自動更新。
/// </summary>
public class BombGaugeUI : MonoBehaviour
{
    [Tooltip("購読対象。未設定時は FindObjectOfType で探索。")]
    public BombController source;
    [Tooltip("プレイヤーカラー(BombController.PlayerCount と同じ長さを推奨)。")]
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.72f, 0.72f, 1f),   // 薄い赤
        new Color(0.72f, 0.83f, 1f, 1f),   // 薄い青
        new Color(0.72f, 1f, 0.72f, 1f),   // 薄い緑
        new Color(1f, 0.93f, 0.72f, 1f),   // 薄い黄
    };
    [Tooltip("画面下端からのオフセット(px)。HP ゲージの少し上に置く。")]
    public float bottomOffset = 90f;
    [Tooltip("各プレイヤー枠の幅(px)。")]
    public float slotWidth = 160f;
    [Tooltip("ボムアイコンのサイズ(px)。")]
    public float iconSize = 22f;

    private bool isVisible = true;
    private int playerCount;
    private int maxBombs;
    private int[] bombCounts;

    // ピクセルテクスチャ
    private Texture2D[] playerLabelTextures;
    private Texture2D[] bombIconTextures;
    private Texture2D[] bombIconDimTextures;
    private Texture2D bgTexture;

    /// <summary>マップ画面など戦闘外で非表示にするための切替。WorldMapPhase が呼ぶ。</summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
    }

    void Awake()
    {
        if (source == null) source = FindObjectOfType<BombController>();
        if (source != null)
        {
            playerCount = source.PlayerCount;
            maxBombs = Mathf.Max(1, source.initialBombsPerPlayer);
            bombCounts = new int[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                bombCounts[i] = source.GetBombs(i);
            }

            source.OnChanged += HandleChanged;
            CreateTextures();
        }
    }

    void OnDestroy()
    {
        if (source != null) source.OnChanged -= HandleChanged;
        DestroyTextures();
    }

    private void HandleChanged(int playerIndex, int count)
    {
        if (bombCounts != null && playerIndex >= 0 && playerIndex < bombCounts.Length)
        {
            bombCounts[playerIndex] = count;
        }
    }

    private void CreateTextures()
    {
        // 背景
        bgTexture = PixelFontRenderer.CreatePixelBox(
            (int)(slotWidth * playerCount) / 4, 12,
            new Color(0.1f, 0.1f, 0.12f, 0.8f),
            new Color(0.5f, 0.5f, 0.55f, 0.9f), 1);

        // プレイヤーラベル
        playerLabelTextures = new Texture2D[playerCount];
        bombIconTextures = new Texture2D[playerCount];
        bombIconDimTextures = new Texture2D[playerCount];

        for (int p = 0; p < playerCount; p++)
        {
            Color pc = (playerColors != null && p < playerColors.Length) ? playerColors[p] : Color.white;

            // "1P", "2P" などのラベル
            playerLabelTextures[p] = PixelFontRenderer.RenderText($"{p + 1}P", pc, 2);

            // ボムアイコン(ピクセル爆弾)
            bombIconTextures[p] = CreatePixelBombIcon(pc);
            bombIconDimTextures[p] = CreatePixelBombIcon(new Color(pc.r * 0.25f, pc.g * 0.25f, pc.b * 0.25f, 0.35f));
        }
    }

    private Texture2D CreatePixelBombIcon(Color color)
    {
        // 8x8 ピクセルの爆弾アイコン
        int size = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0, 0, 0, 0);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        // 爆弾の形状(8x8)
        // 導火線
        pixels[6 * size + 3] = color;
        pixels[6 * size + 4] = color;
        pixels[7 * size + 4] = new Color(1f, 0.6f, 0.2f, color.a); // 火花

        // 本体(丸っぽく)
        pixels[5 * size + 2] = color;
        pixels[5 * size + 3] = color;
        pixels[5 * size + 4] = color;
        pixels[5 * size + 5] = color;

        pixels[4 * size + 1] = color;
        pixels[4 * size + 2] = color;
        pixels[4 * size + 3] = color;
        pixels[4 * size + 4] = color;
        pixels[4 * size + 5] = color;
        pixels[4 * size + 6] = color;

        pixels[3 * size + 1] = color;
        pixels[3 * size + 2] = color;
        pixels[3 * size + 3] = color;
        pixels[3 * size + 4] = color;
        pixels[3 * size + 5] = color;
        pixels[3 * size + 6] = color;

        pixels[2 * size + 1] = color;
        pixels[2 * size + 2] = color;
        pixels[2 * size + 3] = color;
        pixels[2 * size + 4] = color;
        pixels[2 * size + 5] = color;
        pixels[2 * size + 6] = color;

        pixels[1 * size + 2] = color;
        pixels[1 * size + 3] = color;
        pixels[1 * size + 4] = color;
        pixels[1 * size + 5] = color;

        // ハイライト
        Color highlight = new Color(
            Mathf.Min(1f, color.r + 0.3f),
            Mathf.Min(1f, color.g + 0.3f),
            Mathf.Min(1f, color.b + 0.3f),
            color.a);
        pixels[4 * size + 2] = highlight;
        pixels[3 * size + 2] = highlight;

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void DestroyTextures()
    {
        if (bgTexture != null) Destroy(bgTexture);

        if (playerLabelTextures != null)
        {
            foreach (var t in playerLabelTextures) if (t != null) Destroy(t);
        }
        if (bombIconTextures != null)
        {
            foreach (var t in bombIconTextures) if (t != null) Destroy(t);
        }
        if (bombIconDimTextures != null)
        {
            foreach (var t in bombIconDimTextures) if (t != null) Destroy(t);
        }
    }

    void OnGUI()
    {
        if (!isVisible || bombCounts == null) return;

        float totalWidth = slotWidth * playerCount;
        float startX = (Screen.width - totalWidth) / 2f;
        float y = Screen.height - bottomOffset - 46f;

        // 背景
        GUI.DrawTexture(new Rect(startX - 8f, y - 4f, totalWidth + 16f, 54f), bgTexture, ScaleMode.StretchToFill);

        for (int p = 0; p < playerCount; p++)
        {
            float slotX = startX + p * slotWidth;

            // プレイヤーラベル
            if (playerLabelTextures[p] != null)
            {
                GUI.DrawTexture(
                    new Rect(slotX + 4f, y + 8f, playerLabelTextures[p].width, playerLabelTextures[p].height),
                    playerLabelTextures[p]);
            }

            // ボムアイコン
            float iconStartX = slotX + 50f;
            float iconScale = iconSize / 8f; // 8x8のテクスチャをiconSizeにスケール
            int count = bombCounts[p];

            for (int b = 0; b < maxBombs; b++)
            {
                Texture2D iconTex = (b < count) ? bombIconTextures[p] : bombIconDimTextures[p];
                float ix = iconStartX + b * (iconSize + 6f);
                float iy = y + (46f - iconSize) / 2f;
                GUI.DrawTexture(new Rect(ix, iy, iconSize, iconSize), iconTex, ScaleMode.ScaleToFit);
            }
        }
    }
}
