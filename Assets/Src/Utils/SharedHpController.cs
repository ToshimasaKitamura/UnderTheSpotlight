using System;
using UnityEngine;

/// <summary>
/// 弾幕中、4人で共有する1本のHP。被弾時に減少し、0で OnDepleted を発火する。
/// 表示は SharedHpGaugeUI、減算トリガは DodgePhase 経由(PlayerOnFieldHittable.Hit) が担う。
/// </summary>
public class SharedHpController : MonoBehaviour
{
    [Tooltip("最大HP。")]
    public int maxHp = 5;

    [Tooltip("数値の唯一の真実(Excel/CSV連動)。設定で maxHp を上書き。")]
    [SerializeField] private GameTuning tuning;

    public int CurrentHp { get; private set; }

    /// <summary>HP が変化したときに発火。引数は (現在HP, 最大HP)。</summary>
    public event Action<int, int> OnChanged;

    /// <summary>HP が 0 になった瞬間に1回だけ発火。</summary>
    public event Action OnDepleted;

    private bool depletedFired;

    void Awake()
    {
        if (tuning != null) maxHp = tuning.maxHp;
        ResetHp();
    }

    public void ResetHp()
    {
        CurrentHp = Mathf.Max(0, maxHp);
        depletedFired = false;
        OnChanged?.Invoke(CurrentHp, maxHp);
    }

    public void TakeDamage(int amount = 1)
    {
        if (CurrentHp <= 0) return;
        CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, amount));
        OnChanged?.Invoke(CurrentHp, maxHp);
        if (CurrentHp == 0 && !depletedFired)
        {
            depletedFired = true;
            OnDepleted?.Invoke();
        }
    }

    /// <summary>HPを回復する。最大HPを超えない。</summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int before = CurrentHp;
        CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
        if (CurrentHp != before)
        {
            OnChanged?.Invoke(CurrentHp, maxHp);
        }
    }

    /// <summary>HPを直接設定する(デバッグ/特殊処理用)。</summary>
    public void SetHp(int hp)
    {
        CurrentHp = Mathf.Clamp(hp, 0, maxHp);
        OnChanged?.Invoke(CurrentHp, maxHp);
        if (CurrentHp == 0 && !depletedFired)
        {
            depletedFired = true;
            OnDepleted?.Invoke();
        }
    }
}
