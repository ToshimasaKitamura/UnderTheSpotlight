using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// パーティ(プレイヤー人数分)のデッキを束ねるランタイムコンポーネント。
/// 各プレイヤーは独立した PlayerDeck(山札/手札/捨札/強化Lv)を持つ。
/// 戦闘をまたいで存在し続けるためシーンに常駐配置する。
///
/// 数値(人数, ドロー枚数, デッキ構成)は Inspector / アセットで調整。
/// </summary>
public class PartyDeckController : MonoBehaviour
{
    [Header("構成")]
    [Tooltip("プレイヤー人数。")]
    [Min(1)] public int playerCount = 4;
    [Tooltip("各プレイヤーのデッキ定義。要素数が playerCount に満たない場合は末尾(なければ先頭)を流用。")]
    public List<DeckDefinition> perPlayerDecks = new List<DeckDefinition>();
    [Tooltip("乱数シードの基底。プレイヤーごとに +index される(再現性確保用)。0 で時刻ベース。")]
    public int randomSeedBase = 0;

    [Header("ドロー")]
    [Tooltip("一度に引く枚数。アイテム等で増減可。")]
    [Min(1)] public int drawCount = 3;

    [Header("チューニング")]
    [Tooltip("数値の唯一の真実(Excel/CSV連動)。設定で playerCount/drawCount を上書き。")]
    [SerializeField] private GameTuning tuning;

    private PlayerDeck[] decks;

    void Awake()
    {
        if (tuning != null)
        {
            playerCount = tuning.playerCount;
            drawCount = tuning.drawCount;
        }
        BuildDecks();
    }

    public void BuildDecks()
    {
        int n = Mathf.Max(1, playerCount);
        decks = new PlayerDeck[n];
        int baseSeed = randomSeedBase != 0 ? randomSeedBase : System.Environment.TickCount;
        for (int i = 0; i < n; i++)
        {
            decks[i] = new PlayerDeck(ResolveDefinition(i), baseSeed + i);
        }
    }

    private DeckDefinition ResolveDefinition(int playerIndex)
    {
        if (perPlayerDecks == null || perPlayerDecks.Count == 0) return null;
        if (playerIndex < perPlayerDecks.Count && perPlayerDecks[playerIndex] != null)
            return perPlayerDecks[playerIndex];
        // 不足時は末尾→先頭の順で流用
        for (int i = perPlayerDecks.Count - 1; i >= 0; i--)
            if (perPlayerDecks[i] != null) return perPlayerDecks[i];
        return null;
    }

    /// <summary>指定プレイヤーのデッキを取得(範囲外は null)。</summary>
    public PlayerDeck GetDeck(int playerIndex)
    {
        if (decks == null) BuildDecks();
        if (playerIndex < 0 || playerIndex >= decks.Length) return null;
        return decks[playerIndex];
    }

    // ---- ドロー枚数の増減(アイテム効果等) ----
    public void SetDrawCount(int value) => drawCount = Mathf.Max(1, value);
    public void ModifyDrawCount(int delta) => drawCount = Mathf.Max(1, drawCount + delta);
}
