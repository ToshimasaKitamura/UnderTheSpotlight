using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マンガ風吹き出し。トゲトゲの「叫び」スタイル(BurstGraphic)と、その中にテキストを表示する。
/// 通常 PartSelectView などの UI Canvas 配下に生成して使う。
/// 角の数や凹みは Inspector で調整可。
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
    private Text textLabel;

    public string Text
    {
        get => textLabel != null ? textLabel.text : "";
        set { if (textLabel != null) textLabel.text = value; }
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

        // テキスト
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        // 内側にマージン(ギザギザ凹みで切られないように)
        textRt.offsetMin = new Vector2(40f, 30f);
        textRt.offsetMax = new Vector2(-40f, -30f);
        textLabel = textGo.GetComponent<Text>();
        textLabel.font = AcUi.Font();
        textLabel.fontSize = 30;
        textLabel.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        textLabel.alignment = TextAnchor.MiddleCenter;
        textLabel.fontStyle = FontStyle.Bold;
        textLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        textLabel.verticalOverflow = VerticalWrapMode.Overflow;
        textLabel.raycastTarget = false;
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
