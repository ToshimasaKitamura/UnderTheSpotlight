using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 弾幕中のプレイヤー操作権を一定時間ごとに自動ローテーションさせる。
/// CurrentActivePlayer が真のソース。切替時に ActivePlayerChanged を発火する。
/// </summary>
public class PlayerTurnManager : MonoBehaviour
{
    [Tooltip("プレイヤー総数(通常 4)。")]
    public int playerCount = 4;
    [Tooltip("1人あたりの操作時間(秒)。")]
    public float turnDuration = 4f;

    public int CurrentActivePlayer { get; private set; } = -1;

    /// <summary>アクティブプレイヤーが変わったときに発火。引数は新しいインデックス。</summary>
    public event Action<int> ActivePlayerChanged;

    /// <summary>全プレイヤーが1巡し終わった瞬間(=最後のプレイヤーのターン終了)に発火。</summary>
    public event Action RoundCompleted;

    private Coroutine routine;

    public void StartTurns(int startIndex = 0)
    {
        if (routine != null) return;
        SetActive(Mathf.Clamp(startIndex, 0, Mathf.Max(0, playerCount - 1)));
        routine = StartCoroutine(TurnLoop());
    }

    public void StopTurns()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator TurnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(turnDuration);
            if (playerCount <= 0) yield break;
            int next = (CurrentActivePlayer + 1) % playerCount;
            if (next == 0)
            {
                // 最後のプレイヤー(P_n)のターン終了 → 1周完了
                RoundCompleted?.Invoke();
                yield break;
            }
            SetActive(next);
        }
    }

    private void SetActive(int index)
    {
        CurrentActivePlayer = index;
        ActivePlayerChanged?.Invoke(index);
    }

    void OnDestroy()
    {
        StopTurns();
    }
}
