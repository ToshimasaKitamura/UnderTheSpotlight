using System;
using UnityEngine;

/// <summary>
/// 各プレイヤーが保有する援護用ボムの数を管理する常駐コンポーネント。
/// 数値は GameTuning の customParam "bombsPerPlayer" から取得(無ければデフォルト値)。
/// </summary>
public class BombController : MonoBehaviour
{
    [Tooltip("各プレイヤー初期ボム数(GameTuning未設定時のフォールバック)。")]
    public int initialBombsPerPlayer = 2;
    [Tooltip("プレイヤー人数。")]
    [Min(1)] public int playerCount = 4;
    [Tooltip("数値の唯一の真実(任意)。bombsPerPlayer をここから取る。")]
    [SerializeField] private GameTuning tuning;

    private int[] bombs;

    /// <summary>ボム残数が変わったときに発火: (playerIndex, newCount)。</summary>
    public event Action<int, int> OnChanged;
    /// <summary>ボム使用直後に発火: (playerIndex)。</summary>
    public event Action<int> OnUsed;

    public int PlayerCount => bombs != null ? bombs.Length : 0;

    void Awake()
    {
        if (tuning != null)
        {
            playerCount = tuning.playerCount;
            initialBombsPerPlayer = tuning.GetInt("bombsPerPlayer", initialBombsPerPlayer);
        }
        ResetAll();
    }

    public int GetBombs(int playerIndex)
    {
        if (bombs == null || playerIndex < 0 || playerIndex >= bombs.Length) return 0;
        return bombs[playerIndex];
    }

    public void ResetAll()
    {
        int n = Mathf.Max(1, playerCount);
        bombs = new int[n];
        for (int i = 0; i < n; i++) bombs[i] = Mathf.Max(0, initialBombsPerPlayer);
        for (int i = 0; i < n; i++) OnChanged?.Invoke(i, bombs[i]);
    }

    public bool TryUseBomb(int playerIndex)
    {
        if (bombs == null || playerIndex < 0 || playerIndex >= bombs.Length) return false;
        if (bombs[playerIndex] <= 0) return false;
        bombs[playerIndex]--;
        OnChanged?.Invoke(playerIndex, bombs[playerIndex]);
        OnUsed?.Invoke(playerIndex);
        return true;
    }
}
