using UnityEngine;

/// <summary>
/// SpriteRendererベースのHPバー。
/// シーンビューでも確認可能。
/// </summary>
[ExecuteInEditMode]
public class SpriteHpGaugeUI : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("バーの幅（ワールド単位）")]
    public float barWidth = 12f;
    [Tooltip("バーの高さ（ワールド単位）")]
    public float barHeight = 0.8f;
    [Tooltip("画面下からのY座標（ワールド単位）")]
    public float worldY = -7f;

    [Header("色")]
    [Tooltip("HP残量側の色(緑)")]
    public Color fillColor = new Color(0.4f, 0.85f, 0.5f, 1f);
    [Tooltip("背景色(暗紫)")]
    public Color backgroundColor = new Color(0.1f, 0.08f, 0.12f, 0.9f);
    [Tooltip("枠線の色(紫)")]
    public Color borderColor = new Color(0.5f, 0.35f, 0.6f, 1f);
    [Tooltip("危険時(HP 25%以下)の色(赤)")]
    public Color dangerColor = new Color(1f, 0.35f, 0.3f, 1f);

    [Header("参照")]
    [Tooltip("購読対象の HP コントローラ")]
    public SharedHpController hpController;

    // 子オブジェクト
    private GameObject backgroundObj;
    private GameObject fillObj;
    private GameObject borderObj;

    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private SpriteRenderer borderRenderer;

    private int currentHp;
    private int maxHp;

    void Awake()
    {
        if (Application.isPlaying)
        {
            if (hpController == null)
            {
                hpController = FindObjectOfType<SharedHpController>();
            }

            if (hpController != null)
            {
                hpController.OnChanged += HandleHpChanged;
                currentHp = hpController.CurrentHp;
                maxHp = hpController.maxHp;
            }
        }
    }

    void Start()
    {
        CreateVisuals();
        UpdateVisuals();
    }

    void OnDestroy()
    {
        if (hpController != null)
        {
            hpController.OnChanged -= HandleHpChanged;
        }
    }

    void OnValidate()
    {
        // Inspectorで値が変更されたら再描画
        if (backgroundRenderer != null)
        {
            UpdateVisuals();
        }
    }

    void Update()
    {
        // エディタモードでも位置を更新
        if (!Application.isPlaying)
        {
            transform.position = new Vector3(0, worldY, 0);
        }
    }

    private void HandleHpChanged(int current, int max)
    {
        currentHp = current;
        maxHp = max;
        UpdateVisuals();
    }

    private void CreateVisuals()
    {
        // 既存の子オブジェクトをクリア
        foreach (Transform child in transform)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        // 位置設定
        transform.position = new Vector3(0, worldY, 0);

        // 背景
        backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(transform);
        backgroundObj.transform.localPosition = Vector3.zero;
        backgroundRenderer = backgroundObj.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = CreateBoxSprite(barWidth, barHeight, backgroundColor);
        backgroundRenderer.sortingOrder = 100;

        // 枠線
        borderObj = new GameObject("Border");
        borderObj.transform.SetParent(transform);
        borderObj.transform.localPosition = Vector3.zero;
        borderRenderer = borderObj.AddComponent<SpriteRenderer>();
        borderRenderer.sprite = CreateBorderSprite(barWidth, barHeight, borderColor);
        borderRenderer.sortingOrder = 102;

        // HP残量バー
        fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(transform);
        fillObj.transform.localPosition = new Vector3(-barWidth / 2f, 0, 0);
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = CreateBoxSprite(barWidth, barHeight * 0.8f, fillColor);
        fillRenderer.sortingOrder = 101;
    }

    private void UpdateVisuals()
    {
        if (fillRenderer == null) return;

        float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        bool isDanger = ratio <= 0.25f && ratio > 0f;

        // HP残量バーのスケール
        fillObj.transform.localScale = new Vector3(ratio, 1f, 1f);

        // 色の変更
        fillRenderer.color = isDanger ? dangerColor : fillColor;

        // 位置調整（バーが左から伸びるように）
        float fillWidth = barWidth * ratio;
        fillObj.transform.localPosition = new Vector3(-barWidth / 2f + fillWidth / 2f, 0, 0);
    }

    private Sprite CreateBoxSprite(float width, float height, Color color)
    {
        int pixelWidth = Mathf.Max(1, (int)(width * 16));
        int pixelHeight = Mathf.Max(1, (int)(height * 16));

        Texture2D texture = new Texture2D(pixelWidth, pixelHeight);
        texture.filterMode = FilterMode.Point;

        Color[] pixels = new Color[pixelWidth * pixelHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, pixelWidth, pixelHeight),
            new Vector2(0.5f, 0.5f),
            16f
        );
    }

    private Sprite CreateBorderSprite(float width, float height, Color color)
    {
        int pixelWidth = Mathf.Max(1, (int)(width * 16));
        int pixelHeight = Mathf.Max(1, (int)(height * 16));

        Texture2D texture = new Texture2D(pixelWidth, pixelHeight);
        texture.filterMode = FilterMode.Point;

        Color[] pixels = new Color[pixelWidth * pixelHeight];
        int borderThickness = 2;

        for (int y = 0; y < pixelHeight; y++)
        {
            for (int x = 0; x < pixelWidth; x++)
            {
                bool isBorder = x < borderThickness || x >= pixelWidth - borderThickness ||
                                y < borderThickness || y >= pixelHeight - borderThickness;

                int index = y * pixelWidth + x;
                pixels[index] = isBorder ? color : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, pixelWidth, pixelHeight),
            new Vector2(0.5f, 0.5f),
            16f
        );
    }

    // エディタ用のギズモ描画
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 pos = Application.isPlaying ? transform.position : new Vector3(0, worldY, 0);
        Gizmos.DrawWireCube(pos, new Vector3(barWidth, barHeight, 0));
    }
}
