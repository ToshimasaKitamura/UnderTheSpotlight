using System.Collections;
using UnityEngine;

/// <summary>
/// ボス(EnemyOnField)の定期移動パターン。開始位置を中心とした矩形範囲内で
/// 一定間隔でランダムな目標位置を選び、滑らかに移動する。
/// 子オブジェクトの FloatingCharacterMotion とは layer が分かれるので併用可能。
/// </summary>
public class BossMovementPattern : MonoBehaviour
{
    [Tooltip("中心からの左右移動範囲(ワールド単位)。")]
    public float horizontalRange = 250f;
    [Tooltip("中心からの上下移動範囲(ワールド単位)。")]
    public float verticalRange = 60f;
    [Tooltip("次の目標位置に到達するまでの時間(秒)。")]
    public float moveDuration = 1.2f;
    [Tooltip("目標到達後の停止時間(秒)。")]
    public float pauseDuration = 0.4f;
    [Tooltip("最初の移動開始までの遅延(秒)。")]
    public float startDelay = 0.5f;

    private Vector3 center;
    private Coroutine routine;

    public void StartPattern()
    {
        if (routine != null) return;
        center = transform.position;
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

        while (true)
        {
            Vector3 target = center + new Vector3(
                Random.Range(-horizontalRange, horizontalRange),
                Random.Range(-verticalRange, verticalRange),
                0f
            );

            Vector3 from = transform.position;
            float t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / moveDuration);
                transform.position = Vector3.Lerp(from, target, p);
                yield return null;
            }
            transform.position = target;

            if (pauseDuration > 0f) yield return new WaitForSeconds(pauseDuration);
        }
    }

    void OnDestroy()
    {
        StopPattern();
    }
}
