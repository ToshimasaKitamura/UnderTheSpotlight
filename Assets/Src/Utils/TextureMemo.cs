using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OnGUI などで毎フレーム描く飾り(枠・星・文字)のテクスチャを、引数が同じなら使い回すための入れ物。
/// 以前は毎フレーム(OnGUI のイベントごと)に作って Destroy しており、GC と GPU 転送が絶えず発生していた。
///
/// - 返したテクスチャは**このメモが所有**する。受け取った側で Destroy しないこと。
/// - 持ち主のコンポーネントが破棄されるときに <see cref="Dispose"/> を呼ぶ。
/// - 種類が増えすぎた場合(サイズが毎フレーム変わる等)は上限で全部作り直す。
/// </summary>
public class TextureMemo
{
    private const int MaxEntries = 256;
    private readonly Dictionary<string, Texture2D> map = new Dictionary<string, Texture2D>();

    /// <summary>枠付きの四角(PixelFontRenderer.CreatePixelBox と同じ引数)。</summary>
    public Texture2D Box(int width, int height, Color fill, Color border, int borderWidth = 2)
    {
        string key = $"box|{width}|{height}|{fill}|{border}|{borderWidth}";
        return GetOrCreate(key, () => PixelFontRenderer.CreatePixelBox(width, height, fill, border, borderWidth));
    }

    /// <summary>ドット絵の星(PixelFontRenderer.CreatePixelStar と同じ引数)。</summary>
    public Texture2D Star(int size, Color color)
    {
        string key = $"star|{size}|{color}";
        return GetOrCreate(key, () => PixelFontRenderer.CreatePixelStar(size, color));
    }

    /// <summary>文字(PixelFontRenderer.RenderText と同じ引数)。</summary>
    public Texture2D Text(string text, Color color, int scale)
    {
        string key = $"text|{scale}|{color}|{text}";
        return GetOrCreate(key, () => PixelFontRenderer.RenderText(text, color, scale));
    }

    /// <summary>保持しているテクスチャをすべて解放する。</summary>
    public void Dispose()
    {
        foreach (var tex in map.Values) PixelFontRenderer.Release(tex);
        map.Clear();
    }

    private Texture2D GetOrCreate(string key, System.Func<Texture2D> create)
    {
        if (map.TryGetValue(key, out var tex) && tex != null) return tex;
        if (map.Count >= MaxEntries) Dispose(); // 使い捨て用途ではないので、溢れたら作り直す
        tex = create();
        map[key] = tex;
        return tex;
    }
}
