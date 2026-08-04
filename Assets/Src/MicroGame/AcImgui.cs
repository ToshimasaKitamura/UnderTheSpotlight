using UnityEngine;

/// <summary>
/// アクションコマンドの IMGUI(OnGUI) 共通描画ヘルパー。
/// タイマーを「画面中央高さ・一番右(4Pの右上あたり)」に、線で作った金枠付きで表示する。
/// 各ミニゲームの IMGUI ヘルパーから共通利用して見た目を統一する。
/// </summary>
public static class AcImgui
{
    private static readonly Color Gold = new Color(0.82f, 0.67f, 0.33f, 1f);
    private static readonly Color Bg = new Color(0.06f, 0.09f, 0.16f, 0.68f);

    /// <summary>タイマー文字テクスチャを、右中央の金枠に収めて描画する。</summary>
    public static void DrawTimerBox(Texture2D timerTex)
    {
        if (timerTex == null) return;

        float padX = 22f;
        float padY = 14f;
        float boxW = timerTex.width + padX * 2f;
        float boxH = timerTex.height + padY * 2f;

        // 位置: 画面の中央高さ・一番右
        float margin = 28f;
        float boxX = Screen.width - boxW - margin;
        float boxY = Screen.height * 0.45f - boxH * 0.5f;

        Solid(boxX, boxY, boxW, boxH, Bg);          // 下地(半透明濃紺)
        Box(boxX, boxY, boxW, boxH, 3f, Gold);      // 線枠(金)
        GUI.DrawTexture(new Rect(boxX + padX, boxY + padY, timerTex.width, timerTex.height), timerTex);
    }

    /// <summary>塗り矩形。</summary>
    public static void Solid(float x, float y, float w, float h, Color c)
    {
        Color g = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = g;
    }

    /// <summary>太さ t の線で矩形の枠(4辺)を描く。</summary>
    public static void Box(float x, float y, float w, float h, float t, Color c)
    {
        Solid(x, y, w, t, c);            // 上
        Solid(x, y + h - t, w, t, c);    // 下
        Solid(x, y, t, h, c);            // 左
        Solid(x + w - t, y, t, h, c);    // 右
    }
}
