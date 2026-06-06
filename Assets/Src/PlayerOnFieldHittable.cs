using System;
using UnityEngine;

/// <summary>
/// 弾幕の被弾判定対象。DanmakuManager から距離判定で参照される。
/// 被弾時に Hit イベントを発火し、購読側(例: DodgePhase) が HP 減算等を行う。
/// 連続被弾防止のため、被弾後 invulnerabilityDuration 秒は再被弾しない。
/// </summary>
public class PlayerOnFieldHittable : MonoBehaviour
{
    [Tooltip("被弾判定半径(ワールド単位)。")]
    public float radius = 30f;
    [Tooltip("被弾後の無敵時間(秒)。この間は Hit が再発火しない。")]
    public float invulnerabilityDuration = 0.8f;

    /// <summary>無敵期間外に被弾したときに発火。</summary>
    public event Action<PlayerOnFieldHittable> Hit;

    private float invulnerableUntil;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    public void NotifyHit()
    {
        if (IsInvulnerable) return;
        invulnerableUntil = Time.time + invulnerabilityDuration;
        Hit?.Invoke(this);
    }
}
