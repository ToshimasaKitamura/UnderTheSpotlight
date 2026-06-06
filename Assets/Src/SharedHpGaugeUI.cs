using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 共有HPバー UI。画面上部中央にゲージを生成し、SetHp で表示更新する。
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
    [Tooltip("HP残量側の色。")]
    public Color fillColor = new Color(0.3f, 0.85f, 0.35f, 1f);
    [Tooltip("背景色。")]
    public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

    [Tooltip("購読対象の HP コントローラ。未設定時は FindObjectOfType で探索。")]
    public SharedHpController source;

    private RectTransform fillRect;
    private Image fillImage;
    private Text label;
    private GameObject canvasGo;

    void Awake()
    {
        BuildUI();
        if (source == null) source = FindObjectOfType<SharedHpController>();
        if (source != null)
        {
            source.OnChanged += HandleChanged;
            SetHp(source.CurrentHp, source.maxHp);
        }
    }

    /// <summary>マップ画面など戦闘外で非表示にするための切替。WorldMapPhase が呼ぶ。</summary>
    public void SetVisible(bool visible)
    {
        if (canvasGo != null) canvasGo.SetActive(visible);
    }

    void OnDestroy()
    {
        if (source != null) source.OnChanged -= HandleChanged;
    }

    private void HandleChanged(int cur, int max) => SetHp(cur, max);

    private void BuildUI()
    {
        var canvasObj = new GameObject("HpCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        canvasGo = canvasObj;
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var bgObj = new GameObject("HpBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgObj.transform.SetParent(canvasObj.transform, false);
        var bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0f);
        bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0f, bottomOffset);
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);
        bgObj.GetComponent<Image>().color = backgroundColor;

        var fillObj = new GameObject("HpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.transform.SetParent(bgObj.transform, false);
        fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(4f, 0f);
        fillRect.sizeDelta = new Vector2(barWidth - 8f, barHeight - 8f);
        fillImage = fillObj.GetComponent<Image>();
        fillImage.color = fillColor;

        var labelObj = new GameObject("HpLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        labelObj.transform.SetParent(bgObj.transform, false);
        var lr = labelObj.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 0.5f);
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        label = labelObj.GetComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 22;
        label.color = Color.white;
        label.font = ResolveFont();
        var ol = labelObj.GetComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ol.effectDistance = new Vector2(2f, -2f);
    }

    private static Font ResolveFont() => GameFont.Body();

    public void SetHp(int current, int max)
    {
        if (fillRect != null)
        {
            float ratio = (max > 0) ? Mathf.Clamp01((float)current / max) : 0f;
            float w = (barWidth - 8f) * ratio;
            fillRect.sizeDelta = new Vector2(w, barHeight - 8f);
        }
        if (label != null)
        {
            label.text = $"HP  {current} / {max}";
        }
    }
}
