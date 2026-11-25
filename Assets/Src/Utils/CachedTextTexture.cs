using UnityEngine;

/// <summary>
/// PixelFontRenderer.RenderText の結果を1つ保持し、文字・色・スケールが変わったときだけ作り直す小さな入れ物。
/// OnGUI で毎フレーム同じ文字を描く箇所で使う(毎フレーム生成・破棄しないため)。
/// 持ち主のコンポーネントが破棄されるときに <see cref="Dispose"/> を呼んでテクスチャを解放すること。
/// </summary>
public class CachedTextTexture
{
    private Texture2D texture;
    private string text;
    private Color color;
    private int scale;

    /// <summary>指定の文字のテクスチャを返す。前回と同じならそのまま、違えば作り直す。</summary>
    public Texture2D Get(string newText, Color newColor, int newScale)
    {
        if (texture != null && newText == text && newColor == color && newScale == scale) return texture;
        PixelFontRenderer.Release(texture);
        texture = PixelFontRenderer.RenderText(newText, newColor, newScale);
        text = newText;
        color = newColor;
        scale = newScale;
        return texture;
    }

    /// <summary>保持しているテクスチャを解放する。</summary>
    public void Dispose()
    {
        PixelFontRenderer.Release(texture);
        texture = null;
        text = null;
    }
}
