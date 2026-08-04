using System.Collections;
using UnityEngine;

/// <summary>
/// ボス(EnemyOnField)の弾幕パターン。リング状に弾を撃ち、発射ごとに角度オフセットを加える。
/// StartPattern/StopPattern で外部から制御。
/// </summary>
public class BossDanmakuPattern : MonoBehaviour
{
    [Tooltip("弾幕を撃つ DanmakuManager 参照。未設定時はシーン内を検索。")]
    public DanmakuManager danmakuManager;
    [Tooltip("1リング当たりの弾数。")]
    public int bulletsPerRing = 20;
    [Tooltip("発射間隔(秒)。")]
    public float fireInterval = 1.0f;
    [Tooltip("弾速(ワールド単位/秒)。")]
    public float bulletSpeed = 3f;
    [Tooltip("発射ごとに加える角度オフセット(度)。回転する弾幕。")]
    public float ringAngleStepDeg = 7f;
    [Tooltip("最初に撃つまでの遅延(秒)。")]
    public float startDelay = 0.2f;

    [Header("部位破壊による弱化(EnemyRuntimeState から参照)")]
    [Tooltip("WeakenNextAttack 発動時の弾速倍率(1.0 で変化なし)。")]
    [Range(0.1f, 1f)] public float weakenSpeedMul = 0.7f;
    [Tooltip("WeakenNextAttack 発動時の弾数倍率。")]
    [Range(0.1f, 1f)] public float weakenDensityMul = 0.6f;

    private Coroutine routine;
    // StartPattern 時に EnemyRuntimeState を見て弾速/弾数の実効値を確定する。
    private float effectiveBulletSpeed;
    private int effectiveBulletsPerRing;

    public void StartPattern()
    {
        if (routine != null) return;
        if (danmakuManager == null) danmakuManager = FindObjectOfType<DanmakuManager>();
        if (danmakuManager == null)
        {
            Debug.LogWarning("[BossDanmakuPattern] DanmakuManager が見つかりません。");
            return;
        }

        // 既定値
        effectiveBulletSpeed = bulletSpeed;
        effectiveBulletsPerRing = bulletsPerRing;

        // 部位破壊効果を適用
        var state = FindObjectOfType<EnemyRuntimeState>();
        if (state != null)
        {
            if (state.HasActiveEffect(PartDestructionEffect.WeakenNextAttack))
            {
                effectiveBulletSpeed *= weakenSpeedMul;
                effectiveBulletsPerRing = Mathf.Max(4, Mathf.RoundToInt(effectiveBulletsPerRing * weakenDensityMul));
                Debug.Log($"[BossDanmakuPattern] WeakenNextAttack 適用: speed={effectiveBulletSpeed:F1} density={effectiveBulletsPerRing}");
            }
            if (state.HasActiveEffect(PartDestructionEffect.ReducePatternSpeed))
            {
                effectiveBulletSpeed *= 0.75f;
            }
            if (state.HasActiveEffect(PartDestructionEffect.ReducePatternDensity))
            {
                effectiveBulletsPerRing = Mathf.Max(4, Mathf.RoundToInt(effectiveBulletsPerRing * 0.7f));
            }
        }

        routine = StartCoroutine(RunPattern());
    }

    public void StopPattern()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator RunPattern()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        float angleOffset = 0f;
        while (true)
        {
            FireRing(angleOffset);
            angleOffset += ringAngleStepDeg;
            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void FireRing(float angleOffsetDeg)
    {
        Vector3 origin = transform.position;
        int n = Mathf.Max(1, effectiveBulletsPerRing);
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * 360f + angleOffsetDeg;
            float rad = ang * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            danmakuManager.SpawnBullet(origin, dir * effectiveBulletSpeed);
        }
    }

    void OnDestroy()
    {
        StopPattern();
    }
}
