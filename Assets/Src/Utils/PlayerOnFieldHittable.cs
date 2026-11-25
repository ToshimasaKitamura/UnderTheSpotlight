using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弾幕の被弾判定対象。DanmakuManager から距離判定で参照される。
/// 被弾時に Hit イベントを発火し、購読側(例: DodgePhase) が HP 減算等を行う。
/// 連続被弾防止のため、被弾後 invulnerabilityDuration 秒は再被弾しない。
/// </summary>
public class PlayerOnFieldHittable : MonoBehaviour
{
    [Tooltip("被弾判定半径(ワールド単位)。")]
    public float radius = 0.15f;
    [Tooltip("被弾後の無敵時間(秒)。この間は Hit が再発火しない。")]
    public float invulnerabilityDuration = 0.8f;

    /// <summary>無敵期間外に被弾したときに発火。</summary>
    public event Action<PlayerOnFieldHittable> Hit;

    private float invulnerableUntil;

    // 有効な被弾判定対象の一覧。DanmakuManager(毎フレーム)や BossDanmakuPattern(毎発射)が
    // シーン全体を FindObjectsOfType で探さずに済むよう、ここで管理する。
    private static readonly List<PlayerOnFieldHittable> active = new List<PlayerOnFieldHittable>();
    /// <summary>現在有効な被弾判定対象(自機)。</summary>
    public static IReadOnlyList<PlayerOnFieldHittable> Active => active;
    /// <summary>最初の有効な自機(交代制なので通常は1体)。いなければ null。</summary>
    public static PlayerOnFieldHittable First => active.Count > 0 ? active[0] : null;

    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() { active.Remove(this); }

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    /// <summary>
    /// 弾幕デバッグ(デバッグ室の弾幕/弾種検証)中は被弾しない。
    /// 自機を操作せず弾幕を観察するため、ここで被弾を無効化する(弾は DanmakuManager 側ですり抜ける)。
    /// </summary>
    public bool IsDebugInvincible => DebugLaunch.Current == DebugLaunch.Mode.Danmaku;

    public void NotifyHit()
    {
        if (IsDebugInvincible || IsInvulnerable) return;
        invulnerableUntil = Time.time + invulnerabilityDuration;
        Hit?.Invoke(this);
    }
}
