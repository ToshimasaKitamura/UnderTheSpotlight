using System.Collections;
using UnityEngine;

/// <summary>
/// 弾幕回避フェーズにおける敵の出現（EnemyOnField）と弾幕パターンの制御を担当。
/// </summary>
public class DodgeEnemyManager : MonoBehaviour
{
    [Header("設定")]
    public GameObject enemyOnFieldPrefab;
    public Vector3 spawnPosition = new Vector3(0f, 256f, 0f);
    public float entryHeight = 600f;
    public float appearDuration = 0.6f;

    [Header("参照")]
    public DanmakuManager danmakuManager;

    public GameObject Instance { get; private set; }
    public BossDanmakuPattern DanmakuPattern { get; private set; }
    public BossMovementPattern MovementPattern { get; private set; }

    public IEnumerator SpawnAndEnter(EnemyRuntimeState enemyState)
    {
        if (Instance != null) Destroy(Instance);

        Vector3 startPos = spawnPosition + Vector3.up * entryHeight;
        if (enemyOnFieldPrefab != null)
        {
            Instance = Instantiate(enemyOnFieldPrefab, startPos, Quaternion.identity);
        }
        else
        {
            Instance = new GameObject("EnemyOnField_Auto", typeof(SpriteRenderer));
            Instance.transform.position = startPos;
        }

        var controller = Instance.GetComponent<EnemyOnFieldController>() ?? Instance.AddComponent<EnemyOnFieldController>();
        var sr = Instance.GetComponentInChildren<SpriteRenderer>() ?? Instance.AddComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = CreatePlaceholderSprite();
        controller.SetUpSprite(sr.sprite);

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

    private Sprite CreatePlaceholderSprite()
    {
        var tex = new Texture2D(256, 256);
        var cols = new Color32[256 * 256];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
        tex.SetPixels32(cols); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
    }

    public void Cleanup()
    {
        if (Instance != null) Destroy(Instance);
    }
}
