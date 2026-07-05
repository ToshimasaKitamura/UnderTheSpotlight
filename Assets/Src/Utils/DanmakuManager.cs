using System.Collections.Generic;
using UnityEngine;

public class DanmakuManager : MonoBehaviour
{
    public GameObject bulletPrefab;
    public Transform bulletsParent;
    public List<Bullet> bullets = new List<Bullet>();

    [Tooltip("弾を描画する範囲(ワールド座標 AABB)。境界外に出た弾は破棄される。")]
    public Bounds fieldBounds;
    [Tooltip("true で fieldBounds による弾のクリッピングを有効化。")]
    public bool useFieldBounds = false;

    void Update()
    {
        var targets = FindObjectsOfType<PlayerOnFieldHittable>();
        // NotifyHit は購読側(DodgePhase)が ClearAllBullets を同期実行する可能性があるため、
        // 反復はスナップショットで行い、本体 list へは Remove(b) で安全に変更する。
        var snapshot = bullets.ToArray();
        foreach (var b in snapshot)
        {
            if (b == null) { bullets.Remove(b); continue; }

            // フィールド境界外は破棄
            if (useFieldBounds && !IsInsideField2D(b.transform.position))
            {
                Destroy(b.gameObject);
                bullets.Remove(b);
                continue;
            }

            // 自機との衝突判定
            for (int j = 0; j < targets.Length; j++)
            {
                var t = targets[j];
                if (t == null) continue;
                Vector3 diff = b.transform.position - t.transform.position;
                float r = b.radius + t.radius;
                if (diff.sqrMagnitude < r * r)
                {
                    t.NotifyHit();
                    if (b != null) Destroy(b.gameObject);
                    bullets.Remove(b);
                    return; // 被弾でフェーズ終了 — 残り処理は不要
                }
            }
        }
    }

    private bool IsInsideField2D(Vector3 p)
    {
        return p.x >= fieldBounds.min.x && p.x <= fieldBounds.max.x
            && p.y >= fieldBounds.min.y && p.y <= fieldBounds.max.y;
    }

    public GameObject SpawnBullet(Vector3 pos, Vector3 vel)
    {
        if (bulletPrefab == null) return null;
        var go = Instantiate(bulletPrefab, pos, Quaternion.identity, bulletsParent);
        var b = go.GetComponent<Bullet>();
        if (b == null) b = go.AddComponent<Bullet>();
        b.velocity = vel;
        bullets.Add(b);
        return go;
    }

    public void ClearAllBullets()
    {
        for (int i = 0; i < bullets.Count; i++)
        {
            if (bullets[i] != null) Destroy(bullets[i].gameObject);
        }
        bullets.Clear();
    }
}
