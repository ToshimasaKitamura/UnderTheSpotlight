using System;
using UnityEngine;

/// <summary>
/// 弾幕回避フェーズにおける共有HPとゲームオーバー判定を担当。
/// </summary>
public class DodgeHealthManager : MonoBehaviour
{
    public SharedHpController SharedHp { get; private set; }
    public SharedHpGaugeUI HpGauge { get; private set; }

    public event Action HpDepleted;

    public void Initialize(int maxHp)
    {
        SharedHp = FindObjectOfType<SharedHpController>();
        HpGauge = FindObjectOfType<SharedHpGaugeUI>();

        if (SharedHp != null)
        {
            // HP はバトル開始(シーンロード)時にのみ満タンにする。
            // 各弾幕ではリセットせず持ち越し、蓄積したダメージで0になったらゲームオーバー。
            if (SharedHp.maxHp <= 0) SharedHp.maxHp = maxHp;
            SharedHp.OnDepleted -= OnHpDepleted;   // 二重購読防止
            SharedHp.OnDepleted += OnHpDepleted;
        }
    }

    private void OnHpDepleted() => HpDepleted?.Invoke();

    public void TakeDamage(int amount)
    {
        if (SharedHp != null) SharedHp.TakeDamage(amount);
    }

    public void Cleanup()
    {
        if (SharedHp != null) SharedHp.OnDepleted -= OnHpDepleted;
    }
}
