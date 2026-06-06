using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤー切替時に画面中央上に大きなセリフ風 UI を表示するアナウンサー。
/// ランタイムで Canvas + Text を生成し、スケールポップ + フェードで強調。
/// </summary>
public class PlayerSwitchAnnouncer : MonoBehaviour
{
    [Tooltip("各プレイヤーの色(テキスト色に使用)。")]
    public Color[] playerColors;
    [Tooltip("各プレイヤーの掛け声。playerIndex に対応。")]
    public string[] callouts = new string[]
    {
        "1番さん出番ですッ！",
        "2番さん助けて〜！！",
        "3番さん任せたっ！",
        "4番さんお願いっ！",
    };
    [Tooltip("拡大→収束のポップアップ秒数。")]
    public float popDuration = 0.18f;
    [Tooltip("セリフを表示し続ける秒数。")]
    public float holdDuration = 1.4f;
    [Tooltip("フェードアウト秒数。")]
    public float fadeOutDuration = 0.35f;
    [Tooltip("フォントサイズ。")]
    public int fontSize = 96;

    private Canvas canvas;
    private Text text;
    private Outline outline;
    private RectTransform textRect;
    private Coroutine animRoutine;

    void Awake()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        var canvasObj = new GameObject("AnnouncerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var textObj = new GameObject("AnnouncerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        textObj.transform.SetParent(canvasObj.transform, false);
        textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 240f);
        textRect.sizeDelta = new Vector2(1700f, 220f);

        text = textObj.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(1f, 1f, 1f, 0f);
        text.text = "";
        text.font = ResolveFont();

        outline = textObj.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);

        textRect.localScale = Vector3.zero;
    }

    private static Font ResolveFont() => GameFont.Display();

    public void Announce(int playerIndex)
    {
        if (text == null || playerIndex < 0) return;

        string msg = (callouts != null && playerIndex < callouts.Length)
            ? callouts[playerIndex]
            : $"{playerIndex + 1}番さん出番ですッ！";
        Color baseColor = (playerColors != null && playerIndex < playerColors.Length)
            ? playerColors[playerIndex]
            : Color.white;

        text.text = msg;
        var startColor = baseColor; startColor.a = 0f;
        text.color = startColor;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateMessage(baseColor));
    }

    private IEnumerator AnimateMessage(Color baseColor)
    {
        // Phase 1: スケール 0 → 1.25 + フェードイン
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / popDuration);
            float s = Mathf.Lerp(0.2f, 1.25f, EaseOutBack(p));
            textRect.localScale = new Vector3(s, s, 1f);
            var c = baseColor; c.a = p;
            text.color = c;
            yield return null;
        }
        // Phase 2: 1.25 → 1.0 にスムーズ収束
        t = 0f;
        const float settleDuration = 0.12f;
        while (t < settleDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / settleDuration);
            float s = Mathf.Lerp(1.25f, 1.0f, p);
            textRect.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        textRect.localScale = Vector3.one;
        text.color = baseColor;

        // Phase 3: ホールド
        yield return new WaitForSecondsRealtime(holdDuration);

        // Phase 4: フェードアウト
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            var c = baseColor; c.a = 1f - p;
            text.color = c;
            yield return null;
        }
        var endColor = baseColor; endColor.a = 0f;
        text.color = endColor;
        textRect.localScale = Vector3.zero;
        text.text = "";
        animRoutine = null;
    }

    private static float EaseOutBack(float x)
    {
        // 軽いオーバーシュートでポップ感を出す
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = x - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }
}
