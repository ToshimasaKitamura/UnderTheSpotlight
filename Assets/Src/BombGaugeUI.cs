using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 各プレイヤーのボム残数を表示する UI。HP ゲージの上に配置。
/// BombController.OnChanged を購読して自動更新。
/// </summary>
public class BombGaugeUI : MonoBehaviour
{
    [Tooltip("購読対象。未設定時は FindObjectOfType で探索。")]
    public BombController source;
    [Tooltip("プレイヤーカラー(BombController.PlayerCount と同じ長さを推奨)。")]
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.35f, 0.35f, 1f),
        new Color(0.35f, 0.6f, 1f, 1f),
        new Color(0.35f, 1f, 0.4f, 1f),
        new Color(1f, 0.85f, 0.35f, 1f),
    };
    [Tooltip("画面下端からのオフセット(px)。HP ゲージの少し上に置く。")]
    public float bottomOffset = 90f;
    [Tooltip("各プレイヤー枠の幅(px)。")]
    public float slotWidth = 160f;
    [Tooltip("ボムアイコンのサイズ(px)。")]
    public float iconSize = 22f;

    private Canvas canvas;
    private GameObject canvasGo;
    private Image[][] iconImages;

    /// <summary>マップ画面など戦闘外で非表示にするための切替。WorldMapPhase が呼ぶ。</summary>
    public void SetVisible(bool visible)
    {
        if (canvasGo != null) canvasGo.SetActive(visible);
    }

    void Awake()
    {
        if (source == null) source = FindObjectOfType<BombController>();
        BuildCanvas();
        if (source != null)
        {
            BuildIcons(source.PlayerCount, Mathf.Max(1, source.initialBombsPerPlayer));
            source.OnChanged += HandleChanged;
            for (int i = 0; i < source.PlayerCount; i++) HandleChanged(i, source.GetBombs(i));
        }
    }

    void OnDestroy()
    {
        if (source != null) source.OnChanged -= HandleChanged;
    }

    private void BuildCanvas()
    {
        var canvasObj = new GameObject("BombCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        canvasGo = canvasObj;
        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998;
        var s = canvasObj.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;
    }

    private void BuildIcons(int playerCount, int maxBombs)
    {
        int n = Mathf.Max(1, playerCount);
        iconImages = new Image[n][];

        float totalWidth = slotWidth * n;
        float startX = -totalWidth / 2f + slotWidth / 2f;

        for (int p = 0; p < n; p++)
        {
            var slot = new GameObject($"Slot_{p}", typeof(RectTransform));
            slot.transform.SetParent(canvas.transform, false);
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(startX + p * slotWidth, bottomOffset);
            rt.sizeDelta = new Vector2(slotWidth, iconSize + 24f);

            // ラベル
            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            labelObj.transform.SetParent(slot.transform, false);
            var lt = labelObj.GetComponent<Text>();
            lt.text = $"{p + 1}P";
            lt.fontSize = 18;
            lt.fontStyle = FontStyle.Bold;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.font = ResolveFont();
            Color pc = (playerColors != null && p < playerColors.Length) ? playerColors[p] : Color.white;
            lt.color = pc;
            var ol = labelObj.GetComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(2f, -2f);
            var lrt = lt.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(4f, 0f);
            lrt.sizeDelta = new Vector2(40f, 0f);

            // アイコン群
            iconImages[p] = new Image[maxBombs];
            float iconStartX = 50f;
            float spacing = iconSize + 6f;
            for (int b = 0; b < maxBombs; b++)
            {
                var iconObj = new GameObject($"Bomb_{b}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                iconObj.transform.SetParent(slot.transform, false);
                var irt = iconObj.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(iconStartX + b * spacing, 0f);
                irt.sizeDelta = new Vector2(iconSize, iconSize);
                var img = iconObj.GetComponent<Image>();
                img.color = pc;
                var iol = iconObj.GetComponent<Outline>();
                iol.effectColor = new Color(0f, 0f, 0f, 0.85f);
                iol.effectDistance = new Vector2(2f, -2f);
                iconImages[p][b] = img;
            }
        }
    }

    private void HandleChanged(int playerIndex, int count)
    {
        if (iconImages == null || playerIndex < 0 || playerIndex >= iconImages.Length) return;
        var arr = iconImages[playerIndex];
        if (arr == null) return;
        Color pc = (playerColors != null && playerIndex < playerColors.Length) ? playerColors[playerIndex] : Color.white;
        Color dim = new Color(pc.r * 0.25f, pc.g * 0.25f, pc.b * 0.25f, 0.35f);
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null) continue;
            arr[i].color = i < count ? pc : dim;
        }
    }

    private static Font ResolveFont() => GameFont.Body();
}
