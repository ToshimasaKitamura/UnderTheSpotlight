using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マンガ風吹き出し。トゲトゲの「叫び」スタイル(BurstGraphic)と、その中にテキストを表示する。
/// 通常 PartSelectView などの UI Canvas 配下に生成して使う。
/// 角の数や凹みは Inspector で調整可。
/// テキスト描画は PixelFontRenderer + IMGUI を使用。
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    [Tooltip("吹き出し背景色(本体)。")]
    public Color burstColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("外周(輪郭)色。")]
    public Color borderColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("外周の太さ(ピクセル)。")]
    public float borderThickness = 6f;
    [Tooltip("トゲの数(多いほど細かい叫び)。")]
    [Range(6, 32)] public int spikes = 16;
    [Tooltip("凹みの深さ(0..1)。小さいほどギザギザが鋭い。")]
    [Range(0.4f, 0.95f)] public float innerRadiusRatio = 0.78f;

    private RectTransform rt;
    private BurstGraphic border;
    private BurstGraphic fill;
    private string currentText = "";
    private SpeechBubbleIMGUIHelper guiHelper;

    public string Text
    {
        get => currentText;
        set
        {
            currentText = value ?? "";
            if (guiHelper != null) guiHelper.MarkDirty();
        }
    }

    public RectTransform RectTransform => rt;

    /// <summary>引数の親 Canvas 配下に SpeechBubble GameObject を生成して返す。</summary>
    public static SpeechBubble Create(Transform parent, string name = "SpeechBubble")
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<SpeechBubble>();
    }

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        BuildVisual();
    }

    private void BuildVisual()
    {
        // 黒輪郭(少し大きめ)
        var borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer));
        borderGo.transform.SetParent(transform, false);
        var borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-borderThickness, -borderThickness);
        borderRt.offsetMax = new Vector2(borderThickness, borderThickness);
        border = borderGo.AddComponent<BurstGraphic>();
        border.color = borderColor;
        border.spikes = spikes;
        border.innerRadiusRatio = innerRadiusRatio;
        border.raycastTarget = false;

        // 中身
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
        fillGo.transform.SetParent(transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
        fill = fillGo.AddComponent<BurstGraphic>();
        fill.color = burstColor;
        fill.spikes = spikes;
        fill.innerRadiusRatio = innerRadiusRatio;
        fill.raycastTarget = false;

        // IMGUIヘルパーを追加(テキスト描画用)
        guiHelper = gameObject.AddComponent<SpeechBubbleIMGUIHelper>();
        guiHelper.Initialize(this);
    }

    public string GetText() => currentText;
    public Vector2 GetScreenPosition()
    {
        if (rt == null) return new Vector2(Screen.width / 2f, Screen.height / 2f);
        // RectTransformのワールド位置をスクリーン座標に変換
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) / 2f;
        // Canvasがオーバーレイモードの場合はそのまま使える
        return new Vector2(center.x, Screen.height - center.y);
    }
}

/// <summary>SpeechBubble用のIMGUIヘルパー</summary>
public class SpeechBubbleIMGUIHelper : MonoBehaviour
{
    private SpeechBubble bubble;
    private Texture2D textTexture;
    private string cachedText;
    private int pixelScale;
    private bool isDirty = true;

    public void Initialize(SpeechBubble b)
    {
        bubble = b;
        pixelScale = Mathf.Max(3, Screen.height / 240);
    }

    public void MarkDirty()
    {
        isDirty = true;
    }

    void OnDestroy()
    {
        if (textTexture != null) Destroy(textTexture);
    }

    void OnGUI()
    {
        if (bubble == null) return;

        string text = bubble.GetText();
        if (string.IsNullOrEmpty(text)) return;

        // テクスチャ更新
        if (isDirty || text != cachedText || textTexture == null)
        {
            if (textTexture != null) Destroy(textTexture);
            textTexture = PixelFontRenderer.RenderText(text, new Color(0.1f, 0.1f, 0.12f, 1f), pixelScale);
            cachedText = text;
            isDirty = false;
        }

        if (textTexture != null)
        {
            Vector2 pos = bubble.GetScreenPosition();
            float x = pos.x - textTexture.width / 2f;
            float y = pos.y - textTexture.height / 2f;
            GUI.DrawTexture(new Rect(x, y, textTexture.width, textTexture.height), textTexture);
        }
    }
}

/// <summary>マスカブルな星形(叫び)Graphic。OnPopulateMesh で頂点を直接描画する。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class BurstGraphic : MaskableGraphic
{
    [Range(6, 32)] public int spikes = 16;
    [Range(0.3f, 0.95f)] public float innerRadiusRatio = 0.78f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        float w = r.width * 0.5f;
        float h = r.height * 0.5f;
        Vector2 c = r.center;
        int total = Mathf.Max(6, spikes) * 2;

        // 中心 + ふちの頂点を作って三角ファン
        int centerIdx = 0;
        var vert = UIVertex.simpleVert;
        vert.color = color;
        vert.position = new Vector3(c.x, c.y, 0f);
        vh.AddVert(vert);

        for (int i = 0; i < total; i++)
        {
            float rad = (i / (float)total) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            float scale = (i % 2 == 0) ? 1f : innerRadiusRatio;
            vert.position = new Vector3(c.x + Mathf.Cos(rad) * w * scale,
                                        c.y + Mathf.Sin(rad) * h * scale, 0f);
            vh.AddVert(vert);
        }

        for (int i = 0; i < total; i++)
        {
            int a = 1 + i;
            int b = 1 + ((i + 1) % total);
            vh.AddTriangle(centerIdx, a, b);
        }
    }
}
