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
        Debug.Log("[PartyDeckController] Awake 開始");
        if (tuning != null)
        {
            playerCount = tuning.playerCount;
            drawCount = tuning.drawCount;
            Debug.Log($"[PartyDeckController] tuning適用: playerCount={playerCount}, drawCount={drawCount}");
        }
        BuildDecks();
    }

    public void BuildDecks()
    {
        Debug.Log($"[PartyDeckController] BuildDecks 開始: playerCount={playerCount}, perPlayerDecks.Count={perPlayerDecks?.Count ?? 0}");

        // perPlayerDecksが空の場合、Resourcesから自動ロード
        if (perPlayerDecks == null || perPlayerDecks.Count == 0)
        {
            Debug.Log("[PartyDeckController] perPlayerDecksが空のため、Resourcesからデッキを自動ロードします");
            var defaultDeck = CreateDefaultDeck();
            if (defaultDeck != null)
            {
                perPlayerDecks = new List<DeckDefinition>();
                for (int i = 0; i < playerCount; i++)
                {
                    perPlayerDecks.Add(defaultDeck);
                }
                Debug.Log($"[PartyDeckController] デフォルトデッキを{playerCount}人分設定しました: {defaultDeck.name}");
            }
            else
            {
                Debug.LogError("[PartyDeckController] デフォルトデッキのロードに失敗しました！");
            }
        }

        int n = Mathf.Max(1, playerCount);
        decks = new PlayerDeck[n];
        int baseSeed = randomSeedBase != 0 ? randomSeedBase : System.Environment.TickCount;
        for (int i = 0; i < n; i++)
        {
            var def = ResolveDefinition(i);
            if (def == null)
            {
                Debug.LogError($"[PartyDeckController] プレイヤー{i}のデッキ定義がnullです！");
            }
            else
            {
                Debug.Log($"[PartyDeckController] プレイヤー{i}のデッキ: {def.name}, entries={def.entries?.Count ?? 0}");
            }
            decks[i] = new PlayerDeck(def, baseSeed + i);
        }
    }

    private DeckDefinition ResolveDefinition(int playerIndex)
    {
        if (perPlayerDecks == null || perPlayerDecks.Count == 0)
        {
            Debug.LogError($"[PartyDeckController] perPlayerDecksが空です！Inspectorで設定してください。");
            return null;
        }
        if (playerIndex < perPlayerDecks.Count && perPlayerDecks[playerIndex] != null)
            return perPlayerDecks[playerIndex];
        // 不足時は末尾→先頭の順で流用
        for (int i = perPlayerDecks.Count - 1; i >= 0; i--)
            if (perPlayerDecks[i] != null) return perPlayerDecks[i];
        Debug.LogError($"[PartyDeckController] プレイヤー{playerIndex}のデッキが見つかりません！");
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

    /// <summary>
    /// キャラクターデータ配列からデッキを初期化
    /// </summary>
    public void InitializeFromCharacters(CharacterData[] characters)
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning("[PartyDeckController] キャラクターデータが空です。デフォルトデッキを使用します。");
            BuildDecks();
            return;
        }

        playerCount = characters.Length;

        // キャラクターに応じたデッキ定義を取得または作成
        perPlayerDecks.Clear();
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null && characters[i].initialDeck != null && characters[i].initialDeck.Length > 0)
            {
                // CharacterDataのinitialDeckからDeckDefinitionを作成
                var deckDef = CreateDeckDefinitionFromCards(characters[i].initialDeck, characters[i].displayName);
                perPlayerDecks.Add(deckDef);
            }
            else
            {
                // デフォルトデッキ定義を作成
                Debug.LogWarning($"[PartyDeckController] プレイヤー{i + 1}({characters[i]?.displayName})のデッキ定義がありません。デフォルトデッキを使用します。");
                perPlayerDecks.Add(CreateDefaultDeck());
            }
        }

        BuildDecks();
        Debug.Log($"[PartyDeckController] {characters.Length}人のキャラクターからデッキを初期化しました");
    }

    private DeckDefinition CreateDeckDefinitionFromCards(CardData[] cards, string characterName)
    {
        var deckDef = ScriptableObject.CreateInstance<DeckDefinition>();
        deckDef.name = $"{characterName}_Deck";

        // CardDataをDeckDefinition.Entryに変換
        var entries = new List<DeckDefinition.Entry>();
        foreach (var card in cards)
        {
            if (card != null)
            {
                entries.Add(new DeckDefinition.Entry { card = card, copies = 1 });
            }
        }

        deckDef.entries = entries;
        return deckDef;
    }

    private DeckDefinition CreateDefaultDeck()
    {
        // Resources/Data/StandardDeck を探す
        var standardDeck = Resources.Load<DeckDefinition>("Data/StandardDeck");
        if (standardDeck != null)
        {
            Debug.Log($"[PartyDeckController] StandardDeck をロードしました (entries: {standardDeck.entries?.Count ?? 0})");
            return standardDeck;
        }

        Debug.LogError("[PartyDeckController] Resources/Data/StandardDeck.asset が見つかりません！");
        Debug.LogError("[PartyDeckController] 以下を確認してください:");
        Debug.LogError("  1. Assets/Resources/Data/StandardDeck.asset が存在するか");
        Debug.LogError("  2. StandardDeck.asset に entries が設定されているか");
        return null;
    }
}
