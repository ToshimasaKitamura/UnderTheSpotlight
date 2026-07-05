using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ミニゲーム選定演出。PixelFontRendererで8bitドット風フォント表示。
/// 全員が決定キー(Enter/Space/Z) を順に押すと <see cref="AllReady"/> = true。
/// </summary>
public class MiniGameStartView : MonoBehaviour
{
    private int playerCount;
    private int readyIndex;
    private bool isVisible;
    private string gameName;

    // テクスチャキャッシュ
    private Texture2D titleTexture;
    private Texture2D gameNameTexture;
    private Texture2D readyTexture;
    private Texture2D hintTexture;
    private Texture2D bgTexture;

    private int pixelScale;
    private string cachedReadyText = "";

    public bool AllReady => readyIndex >= playerCount;

    public void Show(string gameName, int count, Color[] playerColors)
    {
        playerCount = Mathf.Max(1, count);
        readyIndex = 0;
        this.gameName = gameName;
        isVisible = true;

        pixelScale = Mathf.Max(3, Screen.height / 240);

        CreateTextures();
        RefreshReadyLabel();
    }

    private void CreateTextures()
    {
        ClearTextures();

        // タイトル「MINI GAME」（大きめ黄色）
        titleTexture = PixelFontRenderer.RenderText("MINI GAME", new Color(1f, 0.95f, 0.4f), pixelScale + 3);

        // ゲーム名（白）
        gameNameTexture = PixelFontRenderer.RenderText(gameName, Color.white, pixelScale + 2);

        // ヒント
        hintTexture = PixelFontRenderer.RenderText("Z / SPACE / ENTER", new Color(0.7f, 0.75f, 0.85f), pixelScale);

        // 背景テクスチャ（暗幕）
        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        bgTexture.Apply();
    }

    private void ClearTextures()
    {
        if (titleTexture != null) { Destroy(titleTexture); titleTexture = null; }
        if (gameNameTexture != null) { Destroy(gameNameTexture); gameNameTexture = null; }
        if (readyTexture != null) { Destroy(readyTexture); readyTexture = null; }
        if (hintTexture != null) { Destroy(hintTexture); hintTexture = null; }
        if (bgTexture != null) { Destroy(bgTexture); bgTexture = null; }
        cachedReadyText = "";
    }

    private void RefreshReadyLabel()
    {
        string newText = AllReady
            ? "START!"
            : $"{readyIndex} / {playerCount}";

        if (newText == cachedReadyText && readyTexture != null) return;

        if (readyTexture != null) Destroy(readyTexture);

        Color readyColor = AllReady ? new Color(0.4f, 1f, 0.5f) : new Color(0.85f, 0.9f, 1f);
        readyTexture = PixelFontRenderer.RenderText(newText, readyColor, pixelScale + 1);
        cachedReadyText = newText;
    }

    /// <summary>毎フレーム呼ぶ。全員決定済みなら true。</summary>
    public bool Tick()
    {
        if (AllReady) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame
            || kb.numpadEnterKey.wasPressedThisFrame
            || kb.spaceKey.wasPressedThisFrame
            || kb.zKey.wasPressedThisFrame))
        {
            readyIndex++;
            RefreshReadyLabel();
        }
        return AllReady;
    }

    void OnGUI()
    {
        if (!isVisible) return;

        // 暗幕背景
        if (bgTexture != null)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTexture, ScaleMode.StretchToFill);
        }

        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // タイトル「MINI GAME」
        if (titleTexture != null)
        {
            float x = centerX - titleTexture.width / 2f;
            float y = centerY - 120f;
            GUI.DrawTexture(new Rect(x, y, titleTexture.width, titleTexture.height), titleTexture);
        }

        // ゲーム名
        if (gameNameTexture != null)
        {
            float x = centerX - gameNameTexture.width / 2f;
            float y = centerY - 20f;
            GUI.DrawTexture(new Rect(x, y, gameNameTexture.width, gameNameTexture.height), gameNameTexture);
        }

        // 決定数
        if (readyTexture != null)
        {
            float x = centerX - readyTexture.width / 2f;
            float y = centerY + 80f;
            GUI.DrawTexture(new Rect(x, y, readyTexture.width, readyTexture.height), readyTexture);
        }

        // ヒント
        if (hintTexture != null)
        {
            float x = centerX - hintTexture.width / 2f;
            float y = centerY + 150f;
            GUI.DrawTexture(new Rect(x, y, hintTexture.width, hintTexture.height), hintTexture);
        }
    }

    public void Close()
    {
        isVisible = false;
        ClearTextures();
    }

    void OnDestroy()
    {
        ClearTextures();
    }
}
