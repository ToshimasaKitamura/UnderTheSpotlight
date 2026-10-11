using System.Collections;
using UnityEngine;

/// <summary>
/// 弾幕回避フェーズにおける敵の出現（EnemyOnField）と弾幕パターンの制御を担当。
/// </summary>
public class DodgeEnemyManager : MonoBehaviour
{
    [Header("設定")]
    public GameObject enemyOnFieldPrefab;
    // ワールド座標。フィールドはカメラ中央(0,0)・可視域 y∈[-5,5] なので、上寄りの y=3 に配置。
    public Vector3 spawnPosition = new Vector3(0f, 3f, 0f);
    public float entryHeight = 8f;   // 画面外(上)から降下する高さ(ワールド単位)
    public float appearDuration = 0.6f;

    [Header("参照")]
    public DanmakuManager danmakuManager;
    [Tooltip("弾幕の進行ランプ(ボスindexごとの難易度上昇)に使う。未設定ならランプ無効。")]
    public GameTuning tuning;

    public GameObject Instance { get; private set; }
    public BossDanmakuPattern DanmakuPattern { get; private set; }
    public BossMovementPattern MovementPattern { get; private set; }

    public IEnumerator SpawnAndEnter(EnemyRuntimeState enemyState)
    {
        if (enemyOnFieldPrefab == null)
        {
            Debug.LogError("[DodgeEnemyManager] enemyOnFieldPrefab が未設定です。弾幕ボスのプレハブ(スプライト付き)を Inspector で設定してください。");
            yield break;
        }

        if (Instance != null) Destroy(Instance);

        Vector3 startPos = spawnPosition + Vector3.up * entryHeight;
        Instance = Instantiate(enemyOnFieldPrefab, startPos, Quaternion.identity);

        // 前面に出すため sortingOrder を設定。見た目はボス定義(EnemyData.bossSpriteName)から差し替える。
        // ※ SetUpSprite はルートに別スプライトを足して二重表示になるため呼ばず、既存の Visual 子 SpriteRenderer に直接適用する。
        var sr = Instance.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 10;
            var data = enemyState != null ? enemyState.enemyData : null;
            if (data != null && !string.IsNullOrEmpty(data.bossSpriteName))
            {
                var sprite = BossSpriteResolver.Load(data.bossSpriteName);
                if (sprite != null) sr.sprite = sprite;
                // 見つからない場合は BossSpriteResolver がエラーを出す。プレハブ既定のまま続行。
            }
        }

        float t = 0f;
        while (t < appearDuration)
        {
            t += Time.deltaTime;
            Instance.transform.position = Vector3.Lerp(startPos, spawnPosition, Mathf.SmoothStep(0f, 1f, t / appearDuration));
            yield return null;
        }
        Instance.transform.position = spawnPosition;

        StartPatterns(enemyState);
    }

    private void StartPatterns(EnemyRuntimeState enemyState)
    {
        if (danmakuManager == null) danmakuManager = FindObjectOfType<DanmakuManager>();
        if (danmakuManager == null)
        {
            Debug.LogError("[DodgeEnemyManager] DanmakuManager がシーンにありません。DanmakuManager を配置し、bulletPrefab を設定してください。弾幕を開始できません。");
            return;
        }

        DanmakuPattern = Instance.GetComponent<BossDanmakuPattern>() ?? Instance.AddComponent<BossDanmakuPattern>();
        DanmakuPattern.danmakuManager = danmakuManager;

        // 弾幕パラメータを現在のボス(EnemyData)から確定し、連戦の進行ランプを適用する。
        var data = enemyState != null ? enemyState.enemyData : null;
        var run = FindObjectOfType<RunProgress>();
        int bossIndex = run != null ? run.currentBossIndex : 0;
        float spRamp = tuning != null ? tuning.danmakuSpeedRampPerBoss : 0f;
        float dnRamp = tuning != null ? tuning.danmakuDensityRampPerBoss : 0f;
        DanmakuPattern.Configure(data, bossIndex, spRamp, dnRamp);
        DanmakuPattern.StartPattern();

        MovementPattern = Instance.GetComponent<BossMovementPattern>() ?? Instance.AddComponent<BossMovementPattern>();
        if (enemyState != null && enemyState.HasActiveEffect(PartDestructionEffect.DisableMovement))
        {
            Debug.Log("[DodgeEnemyManager] Movement disabled by effect.");
        }
        else
        {
            MovementPattern.StartPattern();
        }
    }

    public IEnumerator ExitUpward()
    {
        if (Instance == null) yield break;
        Vector3 start = Instance.transform.position;
        Vector3 end = start + Vector3.up * entryHeight;
        float t = 0f;
        while (t < appearDuration)
        {
            t += Time.deltaTime;
            Instance.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / appearDuration));
            yield return null;
        }
        Destroy(Instance);
    }

    public void Cleanup()
    {
        if (Instance != null) Destroy(Instance);
    }
}
