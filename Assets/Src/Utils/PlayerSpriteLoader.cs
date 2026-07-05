using UnityEngine;

/// <summary>
/// プレイヤーキャラクターのスプライトをランタイムで再設定するコンポーネント。
/// シーン内のスプライト参照が壊れている場合のフォールバック処理。
/// </summary>
public class PlayerSpriteLoader : MonoBehaviour
{
    [Tooltip("プレイヤーインデックス（0-7）。このインデックスに対応するスプライトを読み込む。")]
    public int playerIndex = 0;

    [Tooltip("スプライトが読み込めない場合にエラーを出力する")]
    public bool logErrorOnMissing = true;

    private void Start()
    {
        LoadSprite();
    }

    /// <summary>
    /// スプライトを読み込んで設定する
    /// </summary>
    public void LoadSprite()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            if (logErrorOnMissing)
            {
                Debug.LogError($"[PlayerSpriteLoader] {name} にSpriteRendererがありません！");
            }
            return;
        }

        // 既にスプライトが設定されている場合はスキップ
        if (sr.sprite != null)
        {
            Debug.Log($"[PlayerSpriteLoader] {name} のスプライトは既に設定されています: {sr.sprite.name}");
            return;
        }

        // Resources/Images/Player からスプライトシートを読み込む
        var sprites = Resources.LoadAll<Sprite>("Images/Player");
        if (sprites == null || sprites.Length == 0)
        {
            if (logErrorOnMissing)
            {
                Debug.LogError($"[PlayerSpriteLoader] Resources/Images/Player からスプライトを読み込めませんでした！");
            }
            return;
        }

        // Player_X という名前のスプライトを探す
        string targetName = $"Player_{playerIndex}";
        Sprite targetSprite = null;

        foreach (var sprite in sprites)
        {
            if (sprite.name == targetName)
            {
                targetSprite = sprite;
                break;
            }
        }

        if (targetSprite != null)
        {
            sr.sprite = targetSprite;
            Debug.Log($"[PlayerSpriteLoader] {name} にスプライト {targetSprite.name} を設定しました");
        }
        else
        {
            // インデックスでフォールバック
            if (playerIndex < sprites.Length)
            {
                sr.sprite = sprites[playerIndex];
                Debug.Log($"[PlayerSpriteLoader] {name} にスプライト {sprites[playerIndex].name} を設定しました（インデックスフォールバック）");
            }
            else if (logErrorOnMissing)
            {
                Debug.LogError($"[PlayerSpriteLoader] {name} のスプライト {targetName} が見つかりません！利用可能: {string.Join(", ", System.Array.ConvertAll(sprites, s => s.name))}");
            }
        }
    }
}
