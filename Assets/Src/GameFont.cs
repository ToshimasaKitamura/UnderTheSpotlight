using UnityEngine;

/// <summary>
/// プロジェクト全体の UI フォントを 1か所で管理する静的レジストリ。
///
/// 使い方:
///   1. Assets/Fonts/ に好きな .ttf / .otf を入れる(無くても OS フォントにフォールバックするので必須ではない)。
///   2. GameTuning の `displayFont` / `bodyFont` フィールドに Font をドラッグ
///   3. 起動時に <see cref="RegisterFromTuning(GameTuning)"/> を呼ぶ
///   4. 各 UI は <see cref="Display"/>(装飾文字、ヘッダ等) / <see cref="Body"/>(本文) を呼ぶ
///
/// Tuning に Font が無ければ OS の明朝(Display) / ゴシック(Body) に自動フォールバック。
/// </summary>
public static class GameFont
{
    private static Font _displayOverride;
    private static Font _bodyOverride;
    private static Font _displayCached;
    private static Font _bodyCached;

    /// <summary>GameTuning から Font 参照を読み込み、以後 Display/Body の解決時に優先する。</summary>
    public static void RegisterFromTuning(GameTuning tuning)
    {
        if (tuning == null) return;
        if (tuning.displayFont != null) _displayOverride = tuning.displayFont;
        if (tuning.bodyFont != null) _bodyOverride = tuning.bodyFont;
        // キャッシュをクリア(次回 Display/Body で再解決)
        _displayCached = null;
        _bodyCached = null;
    }

    /// <summary>明示的にコードからフォントを上書き(テスト用)。</summary>
    public static void SetDisplay(Font font) { _displayOverride = font; _displayCached = null; }
    public static void SetBody(Font font) { _bodyOverride = font; _bodyCached = null; }

    /// <summary>装飾的フォント(タイトル/ヘッダ等)。未設定なら OS の明朝系を探す。</summary>
    public static Font Display()
    {
        if (_displayOverride != null) return _displayOverride;
        if (_displayCached != null) return _displayCached;
        _displayCached = Font.CreateDynamicFontFromOSFont(
            new[] {
                "Yu Mincho", "Yu Mincho Demibold",
                "Hiragino Mincho ProN", "Hiragino Mincho Pro",
                "MS Mincho", "Noto Serif JP", "Noto Serif",
                "Georgia", "Times New Roman", "Arial"
            }, 48);
        if (_displayCached == null) _displayCached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _displayCached;
    }

    /// <summary>本文フォント(チップ/ラベル等)。未設定なら OS のゴシック系を探す。</summary>
    public static Font Body()
    {
        if (_bodyOverride != null) return _bodyOverride;
        if (_bodyCached != null) return _bodyCached;
        _bodyCached = Font.CreateDynamicFontFromOSFont(
            new[] {
                "Yu Gothic UI", "Yu Gothic",
                "Meiryo", "MS Gothic",
                "Hiragino Sans", "Hiragino Kaku Gothic ProN",
                "Noto Sans JP", "Noto Sans CJK JP",
                "Arial"
            }, 36);
        if (_bodyCached == null) _bodyCached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _bodyCached;
    }
}
