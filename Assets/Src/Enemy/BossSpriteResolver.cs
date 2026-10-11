using UnityEngine;

/// <summary>
/// ボススプライト(Resources/Images/Test の AllChara_N)を名前で解決する共通処理。
/// 登場演出(BossIntroPhase)と弾幕フェーズ(DodgeEnemyManager)の見た目を一致させるために使う。
/// </summary>
public static class BossSpriteResolver
{
    public const string SheetPath = "Images/Test";
    public const string DefaultSpriteName = "AllChara_0";

    /// <summary>
    /// 指定名のボススプライトを読み込む。name が空なら既定(AllChara_0)。
    /// シート未読込や名前不一致は明確なエラーを出して null を返す(安易なフォールバックはしない)。
    /// </summary>
    public static Sprite Load(string spriteName)
    {
        string target = string.IsNullOrEmpty(spriteName) ? DefaultSpriteName : spriteName;
        var sprites = Resources.LoadAll<Sprite>(SheetPath);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError($"[BossSpriteResolver] Resources/{SheetPath} からスプライトを読み込めませんでした。" +
                $"Assets/Resources/Images/Test.png が存在し SpriteMode=Multiple でスライスされているか確認してください。");
            return null;
        }
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null && sprites[i].name == target) return sprites[i];

        Debug.LogError($"[BossSpriteResolver] スプライト '{target}' が見つかりません。" +
            $"EnemyData.bossSpriteName が正しいか確認してください。");
        return null;
    }
}
