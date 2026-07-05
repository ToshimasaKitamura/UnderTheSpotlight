using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スポットライト演出でキャラクターを1人ずつ表示するビュー
/// </summary>
public class CharacterSpotlightView : MonoBehaviour
{
    [Header("メインキャラクター表示")]
    public Image characterImage;
    public Text characterNameText;
    public Text roleText;
    public Text description1Text;
    public Text description2Text;
    public ParticleSystem spotlightParticles;

    [Header("プレイヤー情報表示")]
    public Text currentPlayerText;

    [Header("アニメーション設定")]
    public float transitionDuration = 0.5f;

    private CharacterData currentCharacter;
    private int currentIndex = 0;
    private bool isTransitioning = false;

    public event Action ConfirmSelection;

    public int CurrentIndex => currentIndex;
    public CharacterData CurrentCharacter => currentCharacter;
    public bool IsTransitioning => isTransitioning;

    /// <summary>
    /// キャラクターを即座に表示（初期化用）
    /// </summary>
    public void ShowCharacter(CharacterData character, int index)
    {
        Debug.Log($"[SpotlightView] ShowCharacter({character?.displayName ?? "NULL"}, {index})");
        currentCharacter = character;
        currentIndex = index;
        UpdateDisplay();
    }

    /// <summary>
    /// 次のキャラクターへアニメーション付きで切り替え
    /// </summary>
    public void TransitionToNext(CharacterData nextCharacter, int nextIndex)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionCharacter(nextCharacter, nextIndex, 1));
    }

    /// <summary>
    /// 前のキャラクターへアニメーション付きで切り替え
    /// </summary>
    public void TransitionToPrevious(CharacterData previousCharacter, int previousIndex)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionCharacter(previousCharacter, previousIndex, -1));
    }

    /// <summary>
    /// プレイヤー番号を表示
    /// </summary>
    public void SetCurrentPlayer(int playerNumber)
    {
        if (currentPlayerText != null)
        {
            currentPlayerText.text = $"プレイヤー{playerNumber} キャラクターを選んでください";
        }
    }

    /// <summary>
    /// 決定処理
    /// </summary>
    public void Confirm()
    {
        if (!isTransitioning)
        {
            ConfirmSelection?.Invoke();
        }
    }

    private IEnumerator TransitionCharacter(CharacterData nextCharacter, int nextIndex, int direction)
    {
        isTransitioning = true;

        // 現在のキャラクターをフェードアウト+縮小+回転
        float elapsed = 0f;
        Vector3 originalScale = characterImage.transform.localScale;
        Quaternion originalRotation = characterImage.transform.localRotation;
        Color originalColor = characterImage.color;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            // フェードアウト
            float alpha = 1f - t;
            characterImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            // 縮小
            float scale = 1f - t * 0.5f;
            characterImage.transform.localScale = originalScale * scale;

            // 回転（方向によって左右に回転）
            float rotation = t * 90f * direction;
            characterImage.transform.localRotation = Quaternion.Euler(0, rotation, 0);

            yield return null;
        }

        // キャラクターデータを更新
        currentCharacter = nextCharacter;
        currentIndex = nextIndex;
        UpdateDisplay();

        // 新しいキャラクターをフェードイン+拡大+回転戻し
        elapsed = 0f;
        characterImage.transform.localRotation = Quaternion.Euler(0, -90f * direction, 0);
        characterImage.transform.localScale = originalScale * 0.5f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            // フェードイン
            float alpha = t;
            characterImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            // 拡大
            float scale = 0.5f + t * 0.5f;
            characterImage.transform.localScale = originalScale * scale;

            // 回転戻し
            float rotation = -90f * direction + t * 90f * direction;
            characterImage.transform.localRotation = Quaternion.Euler(0, rotation, 0);

            yield return null;
        }

        // 最終状態を確定
        characterImage.color = originalColor;
        characterImage.transform.localScale = originalScale;
        characterImage.transform.localRotation = originalRotation;

        isTransitioning = false;
    }

    private void UpdateDisplay()
    {
        if (currentCharacter == null) return;

        // キャラクター画像
        if (characterImage != null && currentCharacter.characterSprite != null)
        {
            characterImage.sprite = currentCharacter.characterSprite;
            characterImage.color = Color.white;
            characterImage.preserveAspect = true;
        }

        // キャラクター名
        if (characterNameText != null)
        {
            characterNameText.text = currentCharacter.displayName;
            characterNameText.color = currentCharacter.themeColor;
            
            // Outlineコンポーネントがあれば色を調整
            var outline = characterNameText.GetComponent<Outline>();
            if (outline != null) outline.effectColor = new Color(0, 0, 0, 0.5f);
        }

        // 役割
        if (roleText != null)
        {
            roleText.text = GetRoleText(currentCharacter.role);
            roleText.color = currentCharacter.themeColor;
        }

        // 説明文1
        if (description1Text != null)
        {
            description1Text.text = currentCharacter.description1;
            description1Text.color = Color.white;
        }

        // 説明文2
        if (description2Text != null)
        {
            description2Text.text = currentCharacter.description2;
            description2Text.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        // スポットライトパーティクルの色をテーマカラーに合わせる
        if (spotlightParticles != null)
        {
            var main = spotlightParticles.main;
            Color spotColor = currentCharacter.themeColor;
            spotColor.a = 0.6f;
            main.startColor = spotColor;
            
            // パーティクルを再生
            spotlightParticles.Play();
        }
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
