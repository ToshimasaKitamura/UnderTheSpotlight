using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI ベースのキャラクターカードUI。
/// Image と Text コンポーネントを使用してキャラクター情報を表示。
/// </summary>
public class CharacterCardView : MonoBehaviour
{
    [Header("UI コンポーネント")]
    public Image cardBackground;
    public Image nameBar;
    public Image characterImage;
    public Image roleBackground;

    [Header("テキスト")]
    public Text characterNameText;
    public Text roleText;
    public Text description1Text;
    public Text description2Text;

    [Header("選択状態")]
    public Image selectionBorder;
    public Color normalBorderColor = new Color(0.5f, 0.35f, 0.6f, 1f);
    public Color selectedBorderColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Header("プレイヤーインジケーター")]
    public Image[] playerFrames = new Image[4];

    private bool isSelected = false;
    private float blinkTimer = 0f;
    private bool blinkOn = true;

    private bool[] playersSelecting = new bool[4];

    /// <summary>
    /// カードにキャラクターデータを設定
    /// </summary>
public void SetCharacterData(CharacterData data)
    {
        if (data == null) return;

        // Unity 6 では旧既定フォント(Arial)が無く uGUI Text が空白になるため、規定フォントを明示設定。
        EnsureFont(characterNameText);
        EnsureFont(roleText);
        EnsureFont(description1Text);
        EnsureFont(description2Text);

        // キャラクター名（カードには表示名を大きく載せる）
        if (characterNameText != null)
        {
            characterNameText.text = data.displayName;
        }

        // 名前バーの背景色
        if (nameBar != null)
        {
            nameBar.color = data.themeColor;
        }

        // キャラクタースプライト
        if (characterImage != null && data.characterSprite != null)
        {
            characterImage.sprite = data.characterSprite;
            characterImage.color = Color.white;
            characterImage.preserveAspect = true; // 縦横比を保持
        }

        // 役割テキスト
        if (roleText != null)
        {
            roleText.text = GetRoleText(data.role);
            roleText.color = data.themeColor;
        }

        // 説明文1
        if (description1Text != null)
        {
            description1Text.text = data.description1;
        }

        // 説明文2
        if (description2Text != null)
        {
            description2Text.text = data.description2;
        }
    }

    /// <summary>
    /// 選択状態を設定
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateSelectionVisuals();
    }

    private void Update()
    {
        if (isSelected)
        {
            // 点滅アニメーション
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= 0.3f)
            {
                blinkTimer = 0f;
                blinkOn = !blinkOn;
                UpdateSelectionVisuals();
            }
        }
    }

    /// <summary>
    /// 指定したプレイヤーがこのカードを選択中かどうかを設定
    /// </summary>
    public void SetPlayerSelecting(int playerIndex, bool selecting)
    {
        if (playerIndex < 0 || playerIndex >= playersSelecting.Length) return;
        
        playersSelecting[playerIndex] = selecting;
        
        if (playerFrames != null && playerIndex < playerFrames.Length && playerFrames[playerIndex] != null)
        {
            playerFrames[playerIndex].gameObject.SetActive(selecting);
        }
    }

    private void UpdateSelectionVisuals()
{
        if (selectionBorder != null)
        {
            if (isSelected)
            {
                selectionBorder.color = blinkOn ? selectedBorderColor : selectedBorderColor * 0.7f;
            }
            else
            {
                selectionBorder.color = normalBorderColor;
            }
        }
    }

    // 規定フォント(GameFont=misaki/PixelMplus)を Text に設定。正典: Docs/Specifications/デザインテーマ.md
    private static Font _cardFont;
    private static void EnsureFont(Text t)
    {
        if (t == null) return;
        if (_cardFont == null) _cardFont = GameFont.Body();
        if (_cardFont != null) t.font = _cardFont;
    }

    private static string GetRoleText(CharacterRole role)
    {
        switch (role)
        {
            case CharacterRole.BasicAttacker: return "[基本攻撃]";
            case CharacterRole.BasicDefender: return "[基本防御]";
            case CharacterRole.BasicHealer: return "[基本回復]";
            case CharacterRole.SpecialAttacker: return "[特殊攻撃]";
            case CharacterRole.SpecialDefender: return "[特殊防御]";
            case CharacterRole.SpecialHealer: return "[特殊回復]";
            case CharacterRole.DeckBreaker: return "[デッキ破壊]";
            case CharacterRole.RandomMaster: return "[ランダム要素]";
            default: return "";
        }
    }
}
