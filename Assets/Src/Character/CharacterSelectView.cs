using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャラクター選択画面のビュー（Unity UI / uGUI版）。
/// Canvas上にキャラクターカードを配置し、Grid Layout で管理。
/// シーンに配置されたカードを使用（動的生成なし）。
/// </summary>
public class CharacterSelectView : MonoBehaviour
{
    [Header("UI要素")]
    [Tooltip("シーンに配置されたキャラクターカード（8枚）")]
    public CharacterCardView[] characterCards = new CharacterCardView[8];
    [Tooltip("ヘッダータイトルテキスト")]
    public Text headerText;
    [Tooltip("操作説明テキスト")]
    public Text instructionsText;

    public event Action<int> CharacterSelected;

    public int CursorIndex { get; private set; } = 0;
    public bool IsLocked { get; private set; } = false;

    private readonly List<CharacterData> characters = new List<CharacterData>();

    private void Awake()
    {
        // ヘッダーテキスト
        if (headerText != null)
        {
            headerText.text = "キャラクター選択";
        }

        // 操作説明テキスト
        if (instructionsText != null)
        {
            instructionsText.text = "← → ↑ ↓ Move / Enter 決定";
        }
    }

    /// <summary>
    /// キャラクターリストでビューを構築
    /// </summary>
    public void Build(IList<CharacterData> characterList)
    {
        characters.Clear();
        if (characterList == null || characterList.Count == 0) return;

        foreach (var c in characterList)
        {
            characters.Add(c);
        }

        // シーンに配置されたカードにデータを設定
        SetupCharacterCards();
        CursorIndex = 0;
        IsLocked = false;
        UpdateSelection();
    }

    /// <summary>
    /// シーンに配置されたカードにキャラクターデータを設定
    /// </summary>
    private void SetupCharacterCards()
    {
        for (int i = 0; i < characterCards.Length && i < characters.Count; i++)
        {
            if (characterCards[i] != null)
            {
                characterCards[i].SetCharacterData(characters[i]);
                characterCards[i].gameObject.SetActive(true);
            }
        }

        // データが足りない場合は残りのカードを非表示
        for (int i = characters.Count; i < characterCards.Length; i++)
        {
            if (characterCards[i] != null)
            {
                characterCards[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// カーソルを移動
    /// </summary>
    public void MoveCursor(int deltaX, int deltaY)
    {
        if (IsLocked || characters.Count == 0) return;

        // 常に1次元のリストとして扱う（横一列を想定）
        int delta = deltaX;
        if (delta == 0 && deltaY != 0) delta = deltaY; // 縦入力も横移動に変換

        int newIndex = Mod(CursorIndex + delta, characters.Count);
        
        CursorIndex = newIndex;
        UpdateSelection();
    }

    /// <summary>
    /// 決定
    /// </summary>
    public void Confirm()
    {
        if (IsLocked || characters.Count == 0) return;
        // IsLocked = true; // 次のプレイヤーのためにロックしない
        CharacterSelected?.Invoke(CursorIndex);
    }

    /// <summary>
    /// ロック状態を設定
    /// </summary>
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
    }

    /// <summary>
    /// 全プレイヤーのカーソル位置を設定
    /// </summary>
    public void SetPlayerCursors(int[] cursorIndices)
    {
        if (cursorIndices == null) return;

        // まず全カードの全プレイヤー表示をオフ
        foreach (var card in characterCards)
        {
            if (card == null) continue;
            for (int p = 0; p < 4; p++)
            {
                card.SetPlayerSelecting(p, false);
            }
        }

        // 指定されたプレイヤーの枠をオン
        for (int p = 0; p < cursorIndices.Length; p++)
        {
            int index = cursorIndices[p];
            if (index >= 0 && index < characterCards.Length)
            {
                var card = characterCards[index];
                if (card != null)
                {
                    card.SetPlayerSelecting(p, true);
                }
            }
        }
    }

    /// <summary>
    /// 選択状態を更新
/// </summary>
    private void UpdateSelection()
    {
        for (int i = 0; i < characterCards.Length; i++)
        {
            if (characterCards[i] != null && characterCards[i].gameObject.activeSelf)
            {
                characterCards[i].SetSelected(i == CursorIndex);
            }
        }
    }

    /// <summary>
    /// クリア
    /// </summary>
    public void Clear()
    {
        characters.Clear();

        // 全カードを非表示
        foreach (var card in characterCards)
        {
            if (card != null)
            {
                card.gameObject.SetActive(false);
            }
        }

        IsLocked = false;
        CursorIndex = 0;
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    private void OnDestroy()
    {
        Clear();
    }
}
