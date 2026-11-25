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

    private bool refsResolved;
    private bool refsMissingReported;

    public void Tick(int activePlayerIndex)
    {
        if (!EnsureRefs()) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        // 数字キー 1〜4 = P1〜P4 のボム(手番中のプレイヤー自身は使えない)。仕様: docs/Design/Bomb_Design.md
        if (kb.digit1Key.wasPressedThisFrame) TryUseBomb(0, activePlayerIndex);
        if (kb.digit2Key.wasPressedThisFrame) TryUseBomb(1, activePlayerIndex);
        if (kb.digit3Key.wasPressedThisFrame) TryUseBomb(2, activePlayerIndex);
        if (kb.digit4Key.wasPressedThisFrame) TryUseBomb(3, activePlayerIndex);
    }

    /// <summary>
    /// ボム残数(BombController)と弾の管理(DanmakuManager)の参照を解決する。
    /// どちらもシーンに置かれていない/割り当てられていないことがあるため、ここで探す。
    /// BombController はバトル中ずっと残数を保持する必要があるので、無ければこのオブジェクトに付ける
    /// (このオブジェクト=DodgePhase はバトル中ずっと存在する)。
    /// DanmakuManager が無い場合は弾を消せないので、原因が分かるエラーを1回だけ出してボムを無効にする。
    /// </summary>
    private bool EnsureRefs()
    {
        if (refsResolved && bombController != null && danmakuManager != null) return true;

        if (bombController == null) bombController = FindObjectOfType<BombController>();
        if (bombController == null)
        {
            bombController = gameObject.AddComponent<BombController>();
            Debug.Log($"[DodgeBombManager] シーンに BombController が無いため '{gameObject.name}' に追加しました(ボム残数を保持)。");
        }
        if (danmakuManager == null) danmakuManager = FindObjectOfType<DanmakuManager>();

        if (danmakuManager == null)
        {
            if (!refsMissingReported)
            {
                Debug.LogError("[DodgeBombManager] DanmakuManager がシーンに見つかりません。ボムで弾を消せないため、ボムを無効にします。BattleScene に DanmakuManager を配置してください。");
                refsMissingReported = true;
            }
            return false;
        }

        refsResolved = true;
        return true;
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
