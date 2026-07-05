using UnityEngine;

/// <summary>
/// 自機(白い球)の周囲にプレイヤーカラーのリングを表示する。
/// SetActivePlayer(index) で playerColors[index] に着色して表示、Hide() で非表示。
/// LateUpdate で target の位置に追従。
/// </summary>
public class ActivePlayerRing : MonoBehaviour
{
    [Tooltip("各プレイヤー(P1..P4)のカラー。")]
    public Color[] playerColors;
    [Tooltip("リングを追従させる対象(自機 = 白い球)の Transform。")]
    public Transform target;
    [Tooltip("リングの世界座標サイズ(直径, ワールド単位)。自機より少し大きめに。")]
    public float ringWorldSize = 100f;
    [Tooltip("リング SpriteRenderer の SortingLayer 名。")]
    public string sortingLayerName = "Field";
    [Tooltip("リング SpriteRenderer の sortingOrder。自機(2)・弾(3)より上にする。")]
    public int sortingOrder = 4;

    private SpriteRenderer ringRenderer;
    private GameObject ringVisual;

    void Awake()
    {
        ringVisual = new GameObject("RingVisual", typeof(SpriteRenderer));
        ringVisual.transform.SetParent(transform, false);
        ringRenderer = ringVisual.GetComponent<SpriteRenderer>();
        ringRenderer.sprite = CreateRingSprite(256, 108, 128);
        int layerId = SortingLayer.NameToID(sortingLayerName);
        if (layerId != 0) ringRenderer.sortingLayerID = layerId;
        ringRenderer.sortingOrder = sortingOrder;
        ringVisual.transform.localScale = Vector3.one * (ringWorldSize / 256f);
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position;
    }

    public void SetActivePlayer(int index)
    {
        if (index < 0 || playerColors == null || index >= playerColors.Length || ringRenderer == null)
        {
            gameObject.SetActive(false);
            return;
        }
        ringRenderer.color = playerColors[index];
        gameObject.SetActive(true);
        if (target != null) transform.position = target.position;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private static Sprite CreateRingSprite(int size, int innerRadius, int outerRadius)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        var pixels = new Color32[size * size];
        int cx = size / 2;
        int cy = size / 2;
        int innerSq = innerRadius * innerRadius;
        int outerSq = outerRadius * outerRadius;
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 transparent = new Color32(0, 0, 0, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                int d2 = dx * dx + dy * dy;
                pixels[y * size + x] = (d2 >= innerSq && d2 <= outerSq) ? white : transparent;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }
}
