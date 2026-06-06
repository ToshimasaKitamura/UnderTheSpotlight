using UnityEngine;

/// <summary>
/// カード1種の定義(ScriptableObject)。
/// Project ウィンドウから Create > UnderTheSpotlight > CardData で作成可能。
///
/// 数値はすべて「基本値 + 強化レベル × 1段あたり増分」で実効値を出す。
/// 実効値の算出は CardInstance が担う(ここは定義の保持のみ)。
/// </summary>
[CreateAssetMenu(fileName = "Card", menuName = "UnderTheSpotlight/CardData")]
public class CardData : ScriptableObject
{
    [Header("識別")]
    [Tooltip("内部識別子(効果ハンドラやセーブ等から参照)。")]
    public string id;
    [Tooltip("プレイヤー向け表示名。")]
    public string displayName;
    [TextArea(2, 4)]
    [Tooltip("カード説明文。効果テキスト。")]
    public string description;

    [Header("分類")]
    [Tooltip("行動カテゴリ(攻撃/防御/回復/バフ/特殊)。")]
    public CardCategory category = CardCategory.Attack;
    [Tooltip("レアリティ階層(基本/特殊/必殺)。デッキ構成比に対応。")]
    public CardTier tier = CardTier.Basic;

    [Header("数値(基本値)")]
    [Tooltip("難度修正の基本値。+で難しく、-で易しく(例: 攻撃+3, 回復-1, 特殊+5)。")]
    public int baseDifficultyModifier = 0;
    [Tooltip("威力の基本値(攻撃量/回復量等の基準)。")]
    public int basePower = 1;

    [Header("強化(再シャッフル時に強化レベルが上がる)")]
    [Tooltip("強化レベル1段ごとに加算される難度。")]
    public int difficultyPerUpgrade = 1;
    [Tooltip("強化レベル1段ごとに加算される威力。")]
    public int powerPerUpgrade = 1;

    [Header("効果")]
    [Tooltip("戦闘で適用される効果ロジック種別(現状はデータ保持のみ)。")]
    public CardEffectType effectType = CardEffectType.None;

    [Header("攻撃範囲(部位破壊)")]
    [Tooltip("狙える部位の範囲。Single=任意1部位 / Upper=上半身のみ / Lower=下半身のみ / All=全体。非攻撃カードは All 推奨。")]
    public CardAttackRange attackRange = CardAttackRange.Single;

    [Header("表示")]
    [Tooltip("カード背景色(UI 表示用)。")]
    public Color cardColor = Color.white;
    [Tooltip("カードアイコン(任意)。")]
    public Sprite icon;
}
