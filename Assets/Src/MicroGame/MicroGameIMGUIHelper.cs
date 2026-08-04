using UnityEngine;

/// <summary>
/// マイクロゲーム用IMGUIヘルパー（PixelFontRendererでテキスト描画）
/// テキストを取得するデリゲートを設定し、OnGUIで8bitドット風フォントを描画する。
/// </summary>
public class MicroGameIMGUIHelper : MonoBehaviour
{
    private System.Func<string> getTextFunc;
    private System.Func<string[]> getMultiTextFunc;
    private Color[] textColors;

    private Texture2D cachedTexture;
    private string cachedText;
    private Texture2D[] multiTextures;
    private string[] cachedMultiTexts;
    private int pixelScale;

    // タイマー表示用
    private System.Func<float> getTimeFunc;
    private System.Func<float> getLimitFunc;
    private Texture2D timerTexture;
    private string cachedTimerText;

    /// <summary>単一テキスト用の初期化</summary>
    public void Initialize(System.Func<string> textGetter)
    {
        getTextFunc = textGetter;
        pixelScale = Mathf.Max(3, Screen.height / 240);
    }

    /// <summary>複数テキスト用の初期化（GroupButtonMicroGame等）</summary>
    public void Initialize(System.Func<string[]> multiTextGetter, Color[] colors)
    {
        getMultiTextFunc = multiTextGetter;
        textColors = colors;
        pixelScale = Mathf.Max(3, Screen.height / 240);
    }

    /// <summary>タイマー表示を設定</summary>
    public void SetTimer(System.Func<float> currentTime, System.Func<float> timeLimit)
    {
        getTimeFunc = currentTime;
        getLimitFunc = timeLimit;
    }

    void OnDestroy()
    {
        ClearTextures();
    }

    private void ClearTextures()
    {
        if (cachedTexture != null)
        {
            Destroy(cachedTexture);
            cachedTexture = null;
        }
        if (multiTextures != null)
        {
            foreach (var tex in multiTextures)
                if (tex != null) Destroy(tex);
            multiTextures = null;
        }
        if (timerTexture != null)
        {
            Destroy(timerTexture);
            timerTexture = null;
        }
    }

    void OnGUI()
    {
        // タイマー表示（常に最上部）
        if (getTimeFunc != null && getLimitFunc != null)
        {
            DrawTimer();
        }

        // 単一テキストモード
        if (getTextFunc != null)
        {
            DrawSingleText();
        }
        // 複数テキストモード
        else if (getMultiTextFunc != null)
        {
            DrawMultiText();
        }
    }

    private void DrawSingleText()
    {
        string text = getTextFunc();
        if (string.IsNullOrEmpty(text)) return;

        // テクスチャ更新
        if (text != cachedText || cachedTexture == null)
        {
            if (cachedTexture != null) Destroy(cachedTexture);
            cachedTexture = PixelFontRenderer.RenderText(text, Color.white, pixelScale + 2);
            cachedText = text;
        }

        if (cachedTexture == null) return;

        // 画面中央上部に描画
        float x = (Screen.width - cachedTexture.width) / 2f;
        float y = Screen.height * 0.25f;
        GUI.DrawTexture(new Rect(x, y, cachedTexture.width, cachedTexture.height), cachedTexture);
    }

    private void DrawMultiText()
    {
        string[] texts = getMultiTextFunc();
        if (texts == null || texts.Length == 0) return;

        // テクスチャ更新チェック
        bool needsUpdate = multiTextures == null || multiTextures.Length != texts.Length;
        if (!needsUpdate && cachedMultiTexts != null)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (i >= cachedMultiTexts.Length || texts[i] != cachedMultiTexts[i])
                {
                    needsUpdate = true;
                    break;
                }
            }
        }
        else
        {
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            // 古いテクスチャを破棄
            if (multiTextures != null)
            {
                foreach (var tex in multiTextures)
                    if (tex != null) Destroy(tex);
            }

            multiTextures = new Texture2D[texts.Length];
            cachedMultiTexts = new string[texts.Length];

            for (int i = 0; i < texts.Length; i++)
            {
                Color col = (textColors != null && i < textColors.Length) ? textColors[i] : Color.white;
                multiTextures[i] = PixelFontRenderer.RenderText(texts[i], col, pixelScale + 1);
                cachedMultiTexts[i] = texts[i];
            }
        }

        // 描画（横並び）
        float totalWidth = 0f;
        float maxHeight = 0f;
        float gap = pixelScale * 8;

        foreach (var tex in multiTextures)
        {
            if (tex != null)
            {
                totalWidth += tex.width + gap;
                if (tex.height > maxHeight) maxHeight = tex.height;
            }
        }
        totalWidth -= gap; // 最後のgapを除く

        float startX = (Screen.width - totalWidth) / 2f;
        float y = Screen.height * 0.55f;
        float currentX = startX;

        foreach (var tex in multiTextures)
        {
            if (tex != null)
            {
                GUI.DrawTexture(new Rect(currentX, y, tex.width, tex.height), tex);
                currentX += tex.width + gap;
            }
        }
    }

    private void DrawTimer()
    {
        float currentTime = getTimeFunc();
        float timeLimit = getLimitFunc();
        float remaining = Mathf.Max(0f, timeLimit - currentTime);

        string timerText = $"TIME {remaining:F1}";

        // テクスチャ更新（0.1秒単位で更新）
        if (timerText != cachedTimerText || timerTexture == null)
        {
            if (timerTexture != null) Destroy(timerTexture);

            // 残り時間に応じて色を変更
            Color timerColor = Color.white;
            if (remaining <= 1.0f)
            {
                timerColor = Color.red;
            }
            else if (remaining <= 2.0f)
            {
                timerColor = new Color(1f, 0.6f, 0f); // オレンジ
            }

            timerTexture = PixelFontRenderer.RenderText(timerText, timerColor, pixelScale + 1);
            cachedTimerText = timerText;
        }

        // 右中央・金枠で描画(共通)
        AcImgui.DrawTimerBox(timerTexture);
    }
}
