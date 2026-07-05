using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 回避フェーズの演出（アニメーション）を担当するクラス。
/// 敵の上昇、プレイヤーの退避、フィールドの出現などのコルーチンを管理。
/// </summary>
public class DodgeVisuals : MonoBehaviour
{
    [Header("参照")]
    public Transform enemiesRoot;
    public GameObject centralField;
    public Transform[] playerTransforms;

    [Header("設定")]
    public float enemyRiseDistance = 3f;
    public float enemyRiseDuration = 0.6f;
    public float playerMoveDuration = 0.6f;
    public float fieldScaleDuration = 0.4f;
    public float centralFieldAppearScale = 1f;
    public float playerExitDuration = 0.4f;
    public float playerSideEntryDuration = 0.5f;
    public float playerReturnDuration = 0.6f;
    public float playerSmallScale = 0.5f;
    public float fieldFadeOutDuration = 0.4f;

    // 内部状態
    private Vector3 enemiesOriginalPos;
    private Vector3[] playersOriginalPosArr;
    private Vector3[] playersOriginalScaleArr;
    private List<Transform> individualEnemies;
    private List<Vector3> individualEnemiesOriginalPos;
    private Vector3 centralFieldOriginalScale;

    public void Initialize(Transform enemies, GameObject field, Transform[] players)
    {
        enemiesRoot = enemies;
        centralField = field;
        playerTransforms = players;

        if (enemiesRoot != null) enemiesOriginalPos = enemiesRoot.position;
        if (centralField != null) centralFieldOriginalScale = centralField.transform.localScale;

        if (playerTransforms != null)
        {
            playersOriginalPosArr = new Vector3[playerTransforms.Length];
            playersOriginalScaleArr = new Vector3[playerTransforms.Length];
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                if (playerTransforms[i] != null)
                {
                    playersOriginalPosArr[i] = playerTransforms[i].position;
                    playersOriginalScaleArr[i] = playerTransforms[i].localScale;
                }
            }
        }
    }

    public void SetIndividualEnemies(List<Transform> enemies, List<Vector3> positions)
    {
        individualEnemies = enemies;
        individualEnemiesOriginalPos = positions;
    }

    public IEnumerator PlayEnemyRise()
    {
        float t = 0f;
        while (t < enemyRiseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / enemyRiseDuration);

            if (enemiesRoot != null)
            {
                enemiesRoot.position = Vector3.Lerp(enemiesOriginalPos, enemiesOriginalPos + Vector3.up * enemyRiseDistance, p);
            }
            else if (individualEnemies != null)
            {
                for (int i = 0; i < individualEnemies.Count; i++)
                {
                    if (individualEnemies[i] != null)
                        individualEnemies[i].position = Vector3.Lerp(individualEnemiesOriginalPos[i], individualEnemiesOriginalPos[i] + Vector3.up * enemyRiseDistance, p);
                }
            }
            yield return null;
        }
    }

    public IEnumerator PlayEnemyRestore(float duration, float fromAboveHeight)
    {
        Vector3 aboveOffset = Vector3.up * fromAboveHeight;
        if (enemiesRoot != null)
        {
            Vector3 from = enemiesOriginalPos + aboveOffset;
            enemiesRoot.position = from;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / duration);
                enemiesRoot.position = Vector3.Lerp(from, enemiesOriginalPos, p);
                yield return null;
            }
            enemiesRoot.position = enemiesOriginalPos;
        }
        else if (individualEnemies != null)
        {
            int m = Mathf.Min(individualEnemies.Count, individualEnemiesOriginalPos.Count);
            for (int i = 0; i < m; i++)
                if (individualEnemies[i] != null) individualEnemies[i].position = individualEnemiesOriginalPos[i] + aboveOffset;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / duration);
                for (int i = 0; i < m; i++)
                {
                    if (individualEnemies[i] == null) continue;
                    individualEnemies[i].position = Vector3.Lerp(individualEnemiesOriginalPos[i] + aboveOffset, individualEnemiesOriginalPos[i], p);
                }
                yield return null;
            }
            for (int i = 0; i < m; i++)
                if (individualEnemies[i] != null) individualEnemies[i].position = individualEnemiesOriginalPos[i];
        }
    }

    public IEnumerator PlayFieldAppear()
    {
        if (centralField == null) yield break;
        centralField.SetActive(true);
        SetFieldAlpha(1f);
        float t = 0f;
        Vector3 targetScale = Vector3.one * centralFieldAppearScale;
        while (t < fieldScaleDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / fieldScaleDuration);
            centralField.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, p);
            yield return null;
        }
        centralField.transform.localScale = targetScale;
    }

    public IEnumerator PlayFieldExit()
    {
        if (centralField == null) yield break;
        float t = 0f;
        Vector3 startScale = centralField.transform.localScale;
        while (t < fieldFadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / fieldFadeOutDuration);
            centralField.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
            SetFieldAlpha(1f - p);
            yield return null;
        }
        centralField.SetActive(false);
    }

    public IEnumerator PlayPlayersExitDown()
    {
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        Camera cam = Camera.main;
        float t = 0f;
        while (t < playerExitDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / playerExitDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(playersOriginalPosArr[i]);
                    vp.y = Mathf.Lerp(vp.y, -0.2f, p);
                    vp.z = GetSafeScreenZ(cam, playersOriginalPosArr[i]);
                    Vector3 world = cam.ViewportToWorldPoint(vp);
                    world.z = playersOriginalPosArr[i].z;
                    tr.position = world;
                }
                tr.localScale = Vector3.Lerp(playersOriginalScaleArr[i], playersOriginalScaleArr[i] * playerSmallScale, p);
            }
            yield return null;
        }
    }

    public IEnumerator PlayPlayersRestore()
    {
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        float t = 0f;
        while (t < playerReturnDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / playerReturnDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                tr.position = Vector3.Lerp(tr.position, playersOriginalPosArr[i], p);
                tr.localScale = Vector3.Lerp(tr.localScale, playersOriginalScaleArr[i], p);
            }
            yield return null;
        }
    }

    public void SetFieldAlpha(float a)
    {
        if (centralField == null) return;
        foreach (var sr in centralField.GetComponentsInChildren<SpriteRenderer>(true)) { var c = sr.color; c.a = a; sr.color = c; }
        foreach (var img in centralField.GetComponentsInChildren<Image>(true)) { var c = img.color; c.a = a; img.color = c; }
        foreach (var r in centralField.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var mat in r.materials) if (mat != null && mat.HasProperty("_Color")) { Color c = mat.color; c.a = a; mat.color = c; }
        }
    }

    private float GetSafeScreenZ(Camera cam, Vector3 worldPos)
    {
        if (cam == null) return 10f;
        float z = Vector3.Dot(worldPos - cam.transform.position, cam.transform.forward);
        if (z <= 0.01f) z = Vector3.Distance(cam.transform.position, worldPos);
        return Mathf.Max(z, 0.01f);
    }
}
