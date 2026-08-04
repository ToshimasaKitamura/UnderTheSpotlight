using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤー切替時に画面中央上に大きなセリフ風 UI を表示するアナウンサー。
/// 日本語の文字化けを避けるため、IMGUI ネイティブの GUI.Label(美咲/OSフォント)で描画する。
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
    [Tooltip("フォントスケール(大きめ推奨)。")]
    public int fontScale = 6;

    private Coroutine animRoutine;

    // 描画用の状態
    private string currentMessage = "";
    private Color currentColor = Color.white;
    private float currentScale = 0f;
    private float currentAlpha = 0f;
    private bool isVisible = false;

    // 日本語対応フォント。IMGUI ネイティブ描画(GUI.Label)で使うことで文字化けを防ぐ。
    private Font uiFont;

    void Awake()
    {
        // 8bit風の美咲フォント(日本語)。無ければ本文フォント(OS/GameTuning)にフォールバック。
        uiFont = Resources.Load<Font>("Fonts/misaki_gothic");
        if (uiFont == null) uiFont = GameFont.Body();
    }

    public void Announce(int playerIndex)
    {
        if (playerIndex < 0) return;

        string msg = (callouts != null && playerIndex < callouts.Length)
            ? callouts[playerIndex]
            : $"{playerIndex + 1}番さん出番ですッ！";
        Color baseColor = (playerColors != null && playerIndex < playerColors.Length)
            ? playerColors[playerIndex]
            : Color.white;

        currentMessage = msg;
        currentColor = baseColor;
        currentAlpha = 0f;
        currentScale = 0f;
        isVisible = true;

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
            currentScale = Mathf.Lerp(0.2f, 1.25f, EaseOutBack(p));
            currentAlpha = p;
            yield return null;
        }

        // Phase 2: 1.25 → 1.0 にスムーズ収束
        t = 0f;
        const float settleDuration = 0.12f;
        while (t < settleDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / settleDuration);
            currentScale = Mathf.Lerp(1.25f, 1.0f, p);
            yield return null;
        }
        currentScale = 1.0f;
        currentAlpha = 1.0f;

        // Phase 3: ホールド
        yield return new WaitForSecondsRealtime(holdDuration);

        // Phase 4: フェードアウト
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            currentAlpha = 1f - p;
            yield return null;
        }

        currentAlpha = 0f;
        currentScale = 0f;
        isVisible = false;
        currentMessage = "";
        animRoutine = null;
    }

    void OnGUI()
    {
        if (!isVisible || currentAlpha <= 0f || string.IsNullOrEmpty(currentMessage)) return;

        // IMGUI ネイティブ描画で日本語を正しく表示(文字化け対策)
        int fontSize = Mathf.Max(1, Mathf.RoundToInt(8f * fontScale * Mathf.Max(0.01f, currentScale)));

        var style = new GUIStyle
        {
            font = uiFont,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
        };

        var content = new GUIContent(currentMessage);
        Vector2 sz = style.CalcSize(content);

        float x = (Screen.width - sz.x) / 2f;
        float y = Screen.height * 0.18f;
        var rect = new Rect(x, y, sz.x, sz.y);

        // 影
        var shadow = new GUIStyle(style);
        shadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f * currentAlpha);
        float off = Mathf.Max(2f, fontSize * 0.06f);
        GUI.Label(new Rect(rect.x + off, rect.y + off, rect.width, rect.height), content, shadow);

        // 本体
        style.normal.textColor = new Color(currentColor.r, currentColor.g, currentColor.b, currentAlpha);
        GUI.Label(rect, content, style);
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
