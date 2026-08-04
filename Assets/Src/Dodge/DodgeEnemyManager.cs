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

        // プレハブの見た目(Visual子のスプライト)をそのまま使う。前面に出すため sortingOrder のみ設定。
        // ※ SetUpSprite はルートに別スプライトを足して二重表示になるため呼ばない。サイズはプレハブ側で調整。
        var sr = Instance.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 10;

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
