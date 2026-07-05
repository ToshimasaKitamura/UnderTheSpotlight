using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 各プレイヤーの選択済みキャラクターを表示するリストビュー
/// </summary>
public class PlayerSelectionListView : MonoBehaviour
{
    [System.Serializable]
    public class PlayerSlot
    {
        public GameObject container;
        public Text playerNumberText;
        public Image characterImage;
        public Text characterNameText;
        public Image backgroundPanel;
    }

    [Header("プレイヤースロット（最大4人）")]
    public PlayerSlot[] playerSlots = new PlayerSlot[4];

    /// <summary>
    /// プレイヤーの選択を更新
    /// </summary>
    public void SetPlayerSelection(int playerIndex, CharacterData character)
    {
        if (playerIndex < 0 || playerIndex >= playerSlots.Length) return;

        PlayerSlot slot = playerSlots[playerIndex];
        if (slot == null) return;

        // コンテナを表示
        if (slot.container != null)
        {
            slot.container.SetActive(true);
        }

        // プレイヤー番号
        if (slot.playerNumberText != null)
        {
            slot.playerNumberText.text = $"P{playerIndex + 1}";
        }

        // キャラクター画像
        if (slot.characterImage != null && character != null && character.characterSprite != null)
        {
            slot.characterImage.sprite = character.characterSprite;
            slot.characterImage.color = Color.white;
            slot.characterImage.preserveAspect = true;
        }

        // キャラクター名
        if (slot.characterNameText != null && character != null)
        {
            slot.characterNameText.text = character.displayName;
            slot.characterNameText.color = character.themeColor;
        }

        // 背景パネルの色
        if (slot.backgroundPanel != null && character != null)
        {
            Color panelColor = character.themeColor;
            panelColor.a = 0.3f;
            slot.backgroundPanel.color = panelColor;
        }
    }

    /// <summary>
    /// すべてをクリア（スロット自体は表示したままにする）
    /// </summary>
    public void ClearAll()
    {
        for (int i = 0; i < playerSlots.Length; i++)
        {
            PlayerSlot slot = playerSlots[i];
            if (slot == null || slot.container == null) continue;

            // コンテナは常に表示
            slot.container.SetActive(true);

            // プレイヤー番号を表示
            if (slot.playerNumberText != null)
            {
                slot.playerNumberText.text = $"P{i + 1}";
            }

            // 画像と名前をクリア状態に
            if (slot.characterImage != null)
            {
                slot.characterImage.sprite = null;
                slot.characterImage.color = new Color(0, 0, 0, 0); // 透明
            }

            if (slot.characterNameText != null)
            {
                slot.characterNameText.text = "SELECTING...";
                slot.characterNameText.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
            }

            if (slot.backgroundPanel != null)
            {
                // デフォルトの暗い背景色
                slot.backgroundPanel.color = new Color(0.12f, 0.05f, 0.18f, 0.6f);
            }
        }
    }
}
