using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 弾幕回避フェーズにおけるボム（援護攻撃）の入力と演出を担当。
/// </summary>
public class DodgeBombManager : MonoBehaviour
{
    public BombController bombController;
    public DanmakuManager danmakuManager;
    public Color[] playerColors;

    public void Tick(int activePlayerIndex)
    {
        if (bombController == null || danmakuManager == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) TryUseBomb(0, activePlayerIndex);
        if (kb.digit2Key.wasPressedThisFrame) TryUseBomb(1, activePlayerIndex);
        if (kb.digit3Key.wasPressedThisFrame) TryUseBomb(2, activePlayerIndex);
        if (kb.digit4Key.wasPressedThisFrame) TryUseBomb(4, activePlayerIndex); // Note: Original had digit4Key for player 3 (0-indexed)
    }

    private void TryUseBomb(int playerIndex, int activePlayerIndex)
    {
        if (playerIndex == activePlayerIndex) return;
        if (!bombController.TryUseBomb(playerIndex)) return;

        danmakuManager.ClearAllBullets();
        StartCoroutine(BombFlashRoutine(playerIndex));
    }

    private IEnumerator BombFlashRoutine(int playerIndex)
    {
        Color col = (playerColors != null && playerIndex < playerColors.Length) ? playerColors[playerIndex] : Color.white;
        var go = new GameObject("BombFlash", typeof(SpriteRenderer));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = new Color(col.r, col.g, col.b, 0.6f);
        sr.sortingOrder = 100;
        go.transform.localScale = Vector3.one * 10000f;

        float t = 0f, dur = 0.35f;
        while (t < dur)
        {
            t += Time.deltaTime;
            sr.color = new Color(col.r, col.g, col.b, 0.6f * (1f - t / dur));
            yield return null;
        }
        Destroy(go);
    }

    private Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(2, 2);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
    }
}
