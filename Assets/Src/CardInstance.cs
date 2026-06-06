/// <summary>
/// 山札/手札に存在する実カード(ランタイム)。
/// CardData(定義) を参照しつつ、デッキの「強化レベル」に応じた実効値を提供する。
/// 強化レベルはデッキ1周ごとに DeckController が引き上げ、各 CardInstance に再付与する。
/// </summary>
public sealed class CardInstance
{
    public CardData Data { get; }
    public int UpgradeLevel { get; private set; }

    public CardInstance(CardData data, int upgradeLevel = 0)
    {
        Data = data;
        UpgradeLevel = upgradeLevel < 0 ? 0 : upgradeLevel;
    }

    public void SetUpgradeLevel(int level)
    {
        UpgradeLevel = level < 0 ? 0 : level;
    }

    /// <summary>強化を反映した実効難度修正。</summary>
    public int EffectiveDifficulty =>
        Data.baseDifficultyModifier + Data.difficultyPerUpgrade * UpgradeLevel;

    /// <summary>強化を反映した実効威力。</summary>
    public int EffectivePower =>
        Data.basePower + Data.powerPerUpgrade * UpgradeLevel;

    public CardCategory Category => Data.category;
    public CardTier Tier => Data.tier;
    public CardEffectType EffectType => Data.effectType;
}
