using System.Collections;
using UnityEngine;

/// <summary>
/// 弾幕パターンの種別。ボスごとの個性を出す。
/// </summary>
public enum DanmakuPatternType
{
    /// <summary>全方位リング。発射ごとに回転。</summary>
    Ring = 0,
    /// <summary>自機を狙う扇状の連射(ナイフ投げ的)。</summary>
    AimedFan = 1,
    /// <summary>Ring と AimedFan を一定回数ごとに切り替える(複合)。</summary>
    Mixed = 2,
}

/// <summary>弾の狙い方。偶数弾/奇数弾(自機に弾が来るか隙間が来るか)の作り分けに使う。</summary>
public enum DanmakuAim
{
    /// <summary>自機狙い。Ring は1本が自機へ(奇数弾的)、Fan は自機方向を中心に。</summary>
    Aimed = 0,
    /// <summary>自機外し。自機方向に「隙間」が来るよう半ステップずらす(偶数弾的)。</summary>
    AntiAim = 1,
    /// <summary>固定(真下基準)。自機位置に依存しない。</summary>
    FixedDown = 2,
}

/// <summary>
/// 時間変化する弾幕の1局面。これを並べて「自機狙い→破裂して自機外し全方位」のような
/// 時系列パターンを作る。EnemyData.danmakuPhases に積む。
/// </summary>
[System.Serializable]
public class DanmakuPhase
{
    [Tooltip("デバッグ表示用のラベル。")]
    public string label = "phase";
    [Tooltip("撃ち方。Ring=全方位 / AimedFan=扇。")]
    public DanmakuPatternType shape = DanmakuPatternType.Ring;
    [Tooltip("狙い方。Aimed=自機狙い / AntiAim=自機外し / FixedDown=固定。")]
    public DanmakuAim aim = DanmakuAim.FixedDown;
    [Tooltip("1発射あたりの弾数(= way 数)。偶数/奇数で自機への当たり方が変わる。")]
    public int wayCount = 16;
    [Tooltip("AimedFan の扇の広がり(度)。")]
    public float fanSpreadDeg = 60f;
    [Tooltip("発射ごとに加える回転(度)。渦/スイープ用。")]
    public float rotationStepDeg = 0f;
    [Tooltip("発射間隔(秒)。")]
    public float fireInterval = 0.5f;
    [Tooltip("弾速(ワールド単位/秒)。")]
    public float bulletSpeed = 3f;
    [Tooltip("弾種(色)。Normal/Fast/Heal。")]
    public BulletKind kind = BulletKind.Normal;
    [Tooltip("弾の大きさ。Small/Medium/Large。")]
    public BulletSize size = BulletSize.Medium;
    [Tooltip("弾の形状。Round/Kunai。")]
    public BulletShape bulletShape = BulletShape.Round;
    [Tooltip("この数発ごとに1発を回復弾にする。0で無効。")]
    public int healEveryNShots = 0;
    [Tooltip("この局面を撃ち続ける秒数。経過後に次の局面へ。")]
    public float duration = 1.5f;
}

/// <summary>
/// ボス(EnemyOnField)の弾幕パターン。種別(Ring/AimedFan/Mixed)に応じて撃ち分ける。
/// パラメータは Configure(EnemyData,...) で注入する。StartPattern/StopPattern で制御。
/// </summary>
public class BossDanmakuPattern : MonoBehaviour
{
    [Tooltip("弾幕を撃つ DanmakuManager 参照。未設定時はシーン内を検索。")]
    public DanmakuManager danmakuManager;
    [Tooltip("1回の発射で撃つ弾数(Ring=リングの弾数 / AimedFan=扇の弾数)。")]
    public int bulletsPerRing = 20;
    [Tooltip("発射間隔(秒)。")]
    public float fireInterval = 1.0f;
    [Tooltip("弾速(ワールド単位/秒)。")]
    public float bulletSpeed = 3f;
    [Tooltip("発射ごとに加える角度オフセット(度)。回転する弾幕。")]
    public float ringAngleStepDeg = 7f;
    [Tooltip("最初に撃つまでの遅延(秒)。")]
    public float startDelay = 0.2f;
    [Tooltip("撃つ弾の種別(基本)。Configure(EnemyData) で上書きされる。")]
    public BulletKind bulletKind = BulletKind.Normal;
    [Tooltip("弾幕パターンの種別。Configure(EnemyData) で上書きされる。")]
    public DanmakuPatternType patternType = DanmakuPatternType.Ring;
    [Tooltip("この数だけ弾を撃つごとに1発を回復弾(Heal)にする。0で無効。")]
    public int healEveryNShots = 0;
    [Tooltip("AimedFan の扇の広がり(度)。")]
    public float fanSpreadDeg = 36f;

    [Header("部位破壊による弱化(EnemyRuntimeState から参照)")]
    [Tooltip("WeakenNextAttack 発動時の弾速倍率(1.0 で変化なし)。")]
    [Range(0.1f, 1f)] public float weakenSpeedMul = 0.7f;
    [Tooltip("WeakenNextAttack 発動時の弾数倍率。")]
    [Range(0.1f, 1f)] public float weakenDensityMul = 0.6f;

    private Coroutine routine;
    // StartPattern 時に EnemyRuntimeState を見て弾速/弾数の実効値を確定する。
    private float effectiveBulletSpeed;
    private int effectiveBulletsPerRing;
    private int shotCount; // 回復弾注入のための通算発射数

    // Configure で注入される進行ランプ(連戦のボスindexに応じた難易度上昇)。
    private int rampBossIndex;
    private float speedRampPerBoss;
    private float densityRampPerBoss;
    private float rampSpeedMul = 1f; // StartPattern で確定する弾速倍率(フェーズ弾速にも乗せる)

    // 時間変化フェーズ。非 null かつ要素があればフェーズ駆動、無ければ従来の単一パターン。
    private System.Collections.Generic.List<DanmakuPhase> phases;

    /// <summary>
    /// ボス定義(EnemyData)と進行ランプから弾幕パラメータを確定する。StartPattern の前に呼ぶ。
    /// data が null の場合は Inspector の既定値を使う。
    /// </summary>
    public void Configure(EnemyData data, int bossIndex, float speedRampPerBoss, float densityRampPerBoss)
    {
        if (data != null)
        {
            bulletsPerRing = Mathf.Max(1, data.danmakuBulletsPerRing);
            bulletSpeed = data.danmakuBulletSpeed;
            fireInterval = Mathf.Max(0.05f, data.danmakuFireInterval);
            ringAngleStepDeg = data.danmakuRingAngleStepDeg;
            bulletKind = data.danmakuBulletKind;
            patternType = data.danmakuPattern;
            healEveryNShots = Mathf.Max(0, data.danmakuHealEveryNShots);
            phases = (data.danmakuPhases != null && data.danmakuPhases.Count > 0) ? data.danmakuPhases : null;
        }
        rampBossIndex = Mathf.Max(0, bossIndex);
        this.speedRampPerBoss = speedRampPerBoss;
        this.densityRampPerBoss = densityRampPerBoss;
    }

    public void StartPattern()
    {
        if (routine != null) return;
        if (danmakuManager == null) danmakuManager = FindObjectOfType<DanmakuManager>();
        if (danmakuManager == null)
        {
            Debug.LogWarning("[BossDanmakuPattern] DanmakuManager が見つかりません。");
            return;
        }

        // 既定値 + 進行ランプ(連戦が進むほど速く/多く)
        float speedMul = 1f + speedRampPerBoss * rampBossIndex;
        float densityMul = 1f + densityRampPerBoss * rampBossIndex;
        rampSpeedMul = speedMul;
        effectiveBulletSpeed = bulletSpeed * speedMul;
        effectiveBulletsPerRing = Mathf.Max(1, Mathf.RoundToInt(bulletsPerRing * densityMul));
        shotCount = 0;

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

        // フェーズ指定があれば時間変化パターンを回す(局面を順に、各 duration 秒ずつ、ループ)
        if (phases != null && phases.Count > 0)
        {
            while (true)
            {
                for (int pi = 0; pi < phases.Count; pi++)
                {
                    var p = phases[pi];
                    if (p == null) continue;
                    float interval = Mathf.Max(0.05f, p.fireInterval);
                    float rot = 0f;
                    float elapsed = 0f;
                    do
                    {
                        FirePhase(p, rot);
                        rot += p.rotationStepDeg;
                        elapsed += interval;
                        yield return new WaitForSeconds(interval);
                    } while (elapsed < p.duration);
                }
            }
        }

        float angleOffset = 0f;
        int emission = 0;
        while (true)
        {
            // Mixed は 4 発ごとに Ring と AimedFan を切り替える
            DanmakuPatternType mode = patternType;
            if (patternType == DanmakuPatternType.Mixed)
                mode = ((emission / 4) % 2 == 0) ? DanmakuPatternType.Ring : DanmakuPatternType.AimedFan;

            if (mode == DanmakuPatternType.AimedFan) FireAimedFan();
            else FireRing(angleOffset);

            angleOffset += ringAngleStepDeg;
            emission++;
            yield return new WaitForSeconds(fireInterval);
        }
    }

    /// <summary>全方位リング。</summary>
    private void FireRing(float angleOffsetDeg)
    {
        Vector3 origin = transform.position;
        int n = Mathf.Max(1, effectiveBulletsPerRing);
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * 360f + angleOffsetDeg;
            float rad = ang * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            FireBullet(origin, dir * effectiveBulletSpeed);
        }
    }

    /// <summary>自機を狙う扇状連射。自機が無ければ真下へ。</summary>
    private void FireAimedFan()
    {
        Vector3 origin = transform.position;
        Vector3 aim = Vector3.down;
        var target = FindObjectOfType<PlayerOnFieldHittable>();
        if (target != null)
        {
            Vector3 d = target.transform.position - origin;
            if (d.sqrMagnitude > 0.0001f) aim = d.normalized;
        }
        float baseAng = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        int n = Mathf.Max(1, effectiveBulletsPerRing);
        // n 発を fanSpreadDeg の範囲に均等配置(中心=自機方向)
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0.5f : i / (float)(n - 1);
            float ang = baseAng + Mathf.Lerp(-fanSpreadDeg * 0.5f, fanSpreadDeg * 0.5f, t);
            float rad = ang * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            FireBullet(origin, dir * effectiveBulletSpeed);
        }
    }

    /// <summary>自機方向の角度(度)。自機不明時は真下(-90)。</summary>
    private float AngleToPlayerDeg(Vector3 origin)
    {
        var target = FindObjectOfType<PlayerOnFieldHittable>();
        if (target != null)
        {
            Vector3 d = target.transform.position - origin;
            if (d.sqrMagnitude > 0.0001f) return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }
        return -90f;
    }

    /// <summary>1フェーズ分の発射。shape/aim/way数(偶奇)に応じて方向を決める。</summary>
    private void FirePhase(DanmakuPhase p, float rotDeg)
    {
        Vector3 origin = transform.position;
        int n = Mathf.Max(1, p.wayCount);
        float speed = p.bulletSpeed * rampSpeedMul;
        float aimAng = AngleToPlayerDeg(origin);

        if (p.shape == DanmakuPatternType.AimedFan)
        {
            float center = (p.aim == DanmakuAim.FixedDown) ? -90f : aimAng;
            center += rotDeg;
            float half = p.fanSpreadDeg * 0.5f;
            // AntiAim: 扇の中心に隙間が来るよう半ステップずらす(偶数弾的)
            float antiShift = (p.aim == DanmakuAim.AntiAim && n > 1) ? (p.fanSpreadDeg / (n - 1)) * 0.5f : 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (n == 1) ? 0.5f : i / (float)(n - 1);
                float ang = center + Mathf.Lerp(-half, half, t) + antiShift;
                FirePhaseBullet(origin, ang, speed, p);
            }
        }
        else // Ring(全方位)
        {
            float step = 360f / n;
            float start;
            if (p.aim == DanmakuAim.Aimed) start = aimAng;                  // 1本が自機方向(奇数弾的)
            else if (p.aim == DanmakuAim.AntiAim) start = aimAng + step * 0.5f; // 自機方向に隙間(偶数弾的/自機外し)
            else start = -90f;                                              // FixedDown
            start += rotDeg;
            for (int i = 0; i < n; i++)
                FirePhaseBullet(origin, start + i * step, speed, p);
        }
    }

    private void FirePhaseBullet(Vector3 origin, float angDeg, float speed, DanmakuPhase p)
    {
        shotCount++;
        BulletKind kind = (p.healEveryNShots > 0 && shotCount % p.healEveryNShots == 0)
            ? BulletKind.Heal : p.kind;
        float rad = angDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        danmakuManager.SpawnBullet(origin, dir * speed, kind, p.size, p.bulletShape);
    }

    /// <summary>1発生成。healEveryNShots ごとに回復弾(Heal)にする。</summary>
    private void FireBullet(Vector3 origin, Vector3 velocity)
    {
        shotCount++;
        BulletKind kind = (healEveryNShots > 0 && shotCount % healEveryNShots == 0)
            ? BulletKind.Heal : bulletKind;
        danmakuManager.SpawnBullet(origin, velocity, kind);
    }

    void OnDestroy()
    {
        StopPattern();
    }
}
