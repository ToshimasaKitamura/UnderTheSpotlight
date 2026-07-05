using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤー切替時に画面中央上に大きなセリフ風 UI を表示するアナウンサー。
/// PixelFontRendererを使用して8bitドット風フォントで描画。
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

    // テクスチャキャッシュ
    private Texture2D messageTexture;
    private Texture2D shadowTexture;
    private string cachedMessage = "";
    private Color cachedColor = Color.clear;

    void OnDestroy()
    {
        ClearTextures();
    }

    private void ClearTextures()
    {
        if (messageTexture != null)
        {
            Destroy(messageTexture);
            messageTexture = null;
        }
        if (shadowTexture != null)
        {
            Destroy(shadowTexture);
            shadowTexture = null;
        }
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

        // テクスチャを更新
        UpdateTextures();

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateMessage(baseColor));
    }

    private void UpdateTextures()
    {
        if (currentMessage == cachedMessage && currentColor == cachedColor && messageTexture != null)
            return;

        ClearTextures();

        // メインテクスチャ
        messageTexture = PixelFontRenderer.RenderText(currentMessage, currentColor, fontScale);
        // 影用テクスチャ（黒）
        shadowTexture = PixelFontRenderer.RenderText(currentMessage, new Color(0f, 0f, 0f, 0.85f), fontScale);

        // テクスチャが正しく生成されたか確認
        if (messageTexture == null)
        {
            Debug.LogWarning("[PlayerSwitchAnnouncer] テクスチャの生成に失敗しました");
            isVisible = false;
            return;
        }

        cachedMessage = currentMessage;
        cachedColor = currentColor;
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
        if (!isVisible || messageTexture == null || currentAlpha <= 0f) return;

        // テクスチャサイズにスケール適用
        float w = messageTexture.width * currentScale;
        float h = messageTexture.height * currentScale;

        // 画面中央上部に配置
        float x = (Screen.width - w) / 2f;
        float y = Screen.height * 0.18f; // 上から18%の位置

        // アルファ適用
        Color guiColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, currentAlpha);

        // 影を描画（オフセット）
        if (shadowTexture != null)
        {
            float shadowOffset = 4f * currentScale;
            GUI.DrawTexture(new Rect(x + shadowOffset, y + shadowOffset, w, h), shadowTexture);
        }

        // メインテキスト描画
        GUI.DrawTexture(new Rect(x, y, w, h), messageTexture);

        GUI.color = guiColor;
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
