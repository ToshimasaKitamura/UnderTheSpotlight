using UnityEngine;

/// <summary>
/// 共有HPバー UI（8bit ピクセル風）。
/// OnGUI ベースで描画し、PixelFontRenderer を使用。
/// SharedHpController.OnChanged から呼ばれる想定。
/// </summary>
public class SharedHpGaugeUI : MonoBehaviour
{
    [Tooltip("バーの幅(px)。")]
    public float barWidth = 600f;
    [Tooltip("バーの高さ(px)。")]
    public float barHeight = 36f;
    [Tooltip("画面下端からのオフセット(px)。")]
    public float bottomOffset = 40f;
    [Tooltip("HP残量側の色(緑)。")]
    public Color fillColor = new Color(0.4f, 0.85f, 0.5f, 1f);        // HpFull
    [Tooltip("背景色(暗紫)。")]
    public Color backgroundColor = new Color(0.1f, 0.08f, 0.12f, 0.9f); // HpBackground
    [Tooltip("枠線の色(紫)。")]
    public Color borderColor = new Color(0.5f, 0.35f, 0.6f, 1f);      // FrameOuter
    [Tooltip("危険時(HP 25%以下)の色(赤)。")]
    public Color dangerColor = new Color(1f, 0.35f, 0.3f, 1f);        // HpDanger

    [Tooltip("購読対象の HP コントローラ。未設定時は FindObjectOfType で探索。")]
    public SharedHpController source;

    private int currentHp;
    private int maxHp;
    private bool isVisible = false;

    // ピクセルテクスチャ
    private Texture2D bgTexture;
    private Texture2D fillTexture;
    private Texture2D dangerTexture;
    private Texture2D labelTexture;
    private int lastDisplayedHp = -1;
    private int lastDisplayedMax = -1;

    // 点滅制御(危険時)
    private float blinkTimer;
    private bool blinkOn = true;

    void Awake()
    {
        if (source == null) source = FindObjectOfType<SharedHpController>();
        if (source != null)
        {
            source.OnChanged += HandleChanged;
            currentHp = source.CurrentHp;
            maxHp = source.maxHp;
        }
        CreateTextures();
    }

    /// <summary>マップ画面など戦闘外で非表示にするための切替。WorldMapPhase が呼ぶ。</summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
    }

    void OnDestroy()
    {
        if (source != null) source.OnChanged -= HandleChanged;
        DestroyTextures();
    }

    private void HandleChanged(int cur, int max)
    {
        currentHp = cur;
        maxHp = max;
    }

    public void SetHp(int current, int max)
    {
        currentHp = current;
        maxHp = max;
    }

    private void CreateTextures()
    {
        // 背景(暗いピクセルボックス)
        bgTexture = PixelFontRenderer.CreatePixelBox(
            (int)barWidth / 4, (int)barHeight / 4,
            backgroundColor, borderColor, 1);

        // HP残量バー
        fillTexture = CreateSolidPixelTexture(fillColor);
        dangerTexture = CreateSolidPixelTexture(dangerColor);
    }

    private Texture2D CreateSolidPixelTexture(Color color)
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
        if (bgTexture != null) Destroy(bgTexture);
        if (fillTexture != null) Destroy(fillTexture);
        if (dangerTexture != null) Destroy(dangerTexture);
        if (labelTexture != null) Destroy(labelTexture);
    }

    void Update()
    {
        // 危険時の点滅
        float ratio = (maxHp > 0) ? (float)currentHp / maxHp : 0f;
        if (ratio <= 0.25f && ratio > 0f)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= 0.15f)
            {
                blinkTimer = 0f;
                blinkOn = !blinkOn;
            }
        }
        else
        {
            blinkOn = true;
            blinkTimer = 0f;
        }

        // ラベルテクスチャの更新
        if (currentHp != lastDisplayedHp || maxHp != lastDisplayedMax)
        {
            UpdateLabelTexture();
            lastDisplayedHp = currentHp;
            lastDisplayedMax = maxHp;
        }
    }

    private void UpdateLabelTexture()
    {
        if (labelTexture != null) Destroy(labelTexture);
        string text = $"HP {currentHp}/{maxHp}";
        labelTexture = PixelFontRenderer.RenderText(text, Color.white, 2);
    }

    void OnGUI()
    {
        if (!isVisible) return;

        // 画面中央下にバーを配置
        float x = (Screen.width - barWidth) / 2f;
        float y = Screen.height - bottomOffset - barHeight;

        // 背景
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), bgTexture, ScaleMode.StretchToFill);

        // HP残量バー
        float ratio = (maxHp > 0) ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        float padding = 4f;
        float fillWidth = (barWidth - padding * 2) * ratio;
        float fillHeight = barHeight - padding * 2;

        if (fillWidth > 0)
        {
            bool isDanger = ratio <= 0.25f;
            Texture2D barTex = isDanger ? dangerTexture : fillTexture;

            // 危険時は点滅
            if (!isDanger || blinkOn)
            {
                GUI.DrawTexture(
                    new Rect(x + padding, y + padding, fillWidth, fillHeight),
                    barTex, ScaleMode.StretchToFill);
            }
        }

        // ラベル(中央)
        if (labelTexture != null)
        {
            float labelX = x + (barWidth - labelTexture.width) / 2f;
            float labelY = y + (barHeight - labelTexture.height) / 2f;
            GUI.DrawTexture(new Rect(labelX, labelY, labelTexture.width, labelTexture.height), labelTexture);
        }
    }
}
