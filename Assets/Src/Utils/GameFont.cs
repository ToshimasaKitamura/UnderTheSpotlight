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

    /// <summary>
    /// 本文フォント(チップ/ラベル等)。IMGUI ネイティブで日本語を描画するために、
    /// プロジェクト同梱のインポート済みフォント(misaki_gothic)を最優先で使う。
    ///
    /// ※ Unity 6 の IMGUI(TextCore) は Font.CreateDynamicFontFromOSFont 由来の動的OSフォントを
    ///   FontAsset へ変換する際に FontAsset.CreateFontAsset で例外を投げる(ボタン等のラベルが
    ///   壊れて描画される)。インポート済みの .ttf アセットは安全に変換できるため、そちらを使う。
    /// </summary>
    // 解決順: ドット感 + 可読性を優先。推奨主フォント PixelMplus（要追加）→ 同梱 misaki_gothic。
    // 正典（フォント方針）: Docs/Specifications/デザインテーマ.md
    private static readonly string[] BodyFontCandidates =
    {
        // 決定(主フォント): PixelMplus12。公式配布名(PixelMplus12-Regular)でもリネーム後(PixelMplus12)でも拾えるよう両対応。
        // Assets/Resources/Fonts/ に .ttf を置くだけで自動有効化。
        "Fonts/PixelMplus12-Regular",
        "Fonts/PixelMplus12",
        "Fonts/PixelMplus10-Regular",
        "Fonts/PixelMplus10",
        "Fonts/misaki_gothic",  // 同梱フォールバック(8px 美咲ゴシック)。PixelMplus 未配置時に使用
    };

    public static Font Body()
    {
        if (_bodyOverride != null) return _bodyOverride;
        if (_bodyCached != null) return _bodyCached;

        // インポート済み .ttf を優先(Unity 6 の IMGUI/TextCore でも例外にならない)。
        foreach (var name in BodyFontCandidates)
        {
            _bodyCached = Resources.Load<Font>(name);
            if (_bodyCached != null) return _bodyCached;
        }

        Debug.LogError("[GameFont] Resources/Fonts に本文フォント(PixelMplus / misaki_gothic)が見つかりません。" +
            "日本語 IMGUI ラベルが正しく描画されない可能性があります。" +
            "Assets/Resources/Fonts/ にフォントを配置してください。");

        // 最終手段: IMGUI 組み込みフォント(欧文のみ・例外は出ない)。
        _bodyCached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _bodyCached;
    }
}
