using UnityEngine;

/// <summary>
/// デバッグ用機能(デバッグ室への Esc、即死カード、デバッグ表示、数字キーでの人数指定 など)を
/// 有効にしてよいかの唯一の判定。
///
/// Unity エディタと Development Build でのみ true。製品(リリース)ビルドでは false になり、
/// 通常プレイでデバッグ機能が動かない。デバッグ機能を追加するときは必ずこれで囲む。
/// 仕様: docs/Git運用.md「リリース」のリリース前チェック。
/// </summary>
public static class DebugFeatures
{
    /// <summary>デバッグ機能を有効にしてよいか(エディタ / Development Build)。</summary>
    public static bool Enabled => Debug.isDebugBuild;
}
