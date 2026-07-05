using UnityEngine;

/// <summary>
/// キャラクターの種類
/// </summary>
public enum CharacterType
{
    Iruka,           // イルカ - 基本的な攻撃キャラ
    Neko,            // ネコ - 基本的な防御キャラ
    Hitsji,          // ヒッジ - 基本的な回復キャラ
    Nishikawa,       // ニシカワ・ホットワード - デッキ破壊特殊キャラ
    Ratel,           // ラーテル - ハイリスク攻撃キャラ
    Pierrot,         // ピエロ - 特殊防御キャラ
    Akuma,           // アクマ - 特殊回復キャラ
    Teacher          // スガクティーチャー - ランダム要素キャラ
}

/// <summary>
/// キャラクターの特徴・役割
/// </summary>
public enum CharacterRole
{
    BasicAttacker,   // 基本的な攻撃キャラ
    BasicDefender,   // 基本的な防御キャラ
    BasicHealer,     // 基本的な回復キャラ
    SpecialAttacker, // 特殊な攻撃キャラ
    SpecialDefender, // 特殊な防御キャラ
    SpecialHealer,   // 特殊な回復キャラ
    DeckBreaker,     // デッキ破壊特殊キャラ
    RandomMaster     // ランダム要素マスター
}

/// <summary>
/// キャラクターデータ（ScriptableObject）
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("キャラクターの種類")]
    public CharacterType characterType;

    [Tooltip("表示名")]
    public string displayName;

    [Tooltip("ローマ字名")]
    public string romajiName;

    [Tooltip("キャラクターの役割")]
public CharacterRole role;

    [Tooltip("キャラクターの説明（1行目）")]
    public string description1;

    [Tooltip("キャラクターの説明（2行目）")]
    public string description2;

    [Header("ビジュアル")]
    [Tooltip("キャラクターのスプライト")]
    public Sprite characterSprite;

    [Tooltip("キャラクターのテーマカラー")]
    public Color themeColor = Color.white;

    [Header("カードデッキ")]
    [Tooltip("このキャラクターの初期デッキに含まれるカード")]
    public CardData[] initialDeck;

    [Tooltip("このキャラクターが使用する主なカードタイプの説明")]
    public string deckDescription;
}
