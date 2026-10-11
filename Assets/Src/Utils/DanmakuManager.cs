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

    [Tooltip("回復弾(Heal)命中時に回復させるボスの状態。未設定時はシーン内を検索。")]
    public EnemyRuntimeState enemyState;

    // 弾種ごとの色(基本は白黒。用途で見た目を変える)。仕様: docs/Design/Danmaku_Design.md
    private static readonly Color NormalColor = Color.white;
    private static readonly Color FastColor = new Color(0.60f, 0.85f, 1f, 1f);   // 速弾(クナイ)=淡い青
    private static readonly Color HealColor = new Color(0.40f, 0.851f, 0.498f, 1f); // 回復弾=緑(Success)

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
                    if (b.kind == BulletKind.Heal)
                    {
                        // 回復弾: 自機ダメージ無し。代わりにボスが自己回復する(避けるべき弾)。
                        ApplyBossHeal(b.healAmount);
                        Destroy(b.gameObject);
                        bullets.Remove(b);
                        break; // 他の自機(交代中は1体だが将来の多体)への二重命中を防ぐ
                    }
                    t.NotifyHit();
                    if (b != null) Destroy(b.gameObject);
                    bullets.Remove(b);
                    return; // 被弾でフェーズ終了 — 残り処理は不要
                }
            }
        }
    }

    private void ApplyBossHeal(int amount)
    {
        if (amount <= 0) return;
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (enemyState == null)
        {
            Debug.LogWarning("[DanmakuManager] 回復弾が命中しましたが EnemyRuntimeState が見つからず、ボスを回復できません。");
            return;
        }
        enemyState.HealAny(amount);
    }

    private bool IsInsideField2D(Vector3 p)
    {
        return p.x >= fieldBounds.min.x && p.x <= fieldBounds.max.x
            && p.y >= fieldBounds.min.y && p.y <= fieldBounds.max.y;
    }

    private bool warnedNoBulletPrefab;

    /// <summary>通常弾(白黒)を生成する。弾種を指定したい場合は overload を使う。</summary>
    public GameObject SpawnBullet(Vector3 pos, Vector3 vel)
    {
        return SpawnBullet(pos, vel, BulletKind.Normal);
    }

    /// <summary>弾種を指定して弾を生成する。</summary>
    public GameObject SpawnBullet(Vector3 pos, Vector3 vel, BulletKind kind)
    {
        if (bulletPrefab == null)
        {
            if (!warnedNoBulletPrefab)
            {
                Debug.LogError("[DanmakuManager] bulletPrefab が未設定です。弾のプレハブ(Bullet コンポーネント付き)を設定してください。弾を生成できません。");
                warnedNoBulletPrefab = true;
            }
            return null;
        }

        var go = Instantiate(bulletPrefab, pos, Quaternion.identity, bulletsParent);
        var b = go.GetComponent<Bullet>();
        if (b == null) b = go.AddComponent<Bullet>();
        b.velocity = vel;
        b.kind = kind;
        ApplyKindVisual(go, kind);
        bullets.Add(b);
        return go;
    }

    /// <summary>弾種に応じて弾の見た目(色)を設定する。</summary>
    private void ApplyKindVisual(GameObject bulletGo, BulletKind kind)
    {
        var sr = bulletGo.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        switch (kind)
        {
            case BulletKind.Fast: sr.color = FastColor; break;
            case BulletKind.Heal: sr.color = HealColor; break;
            default: sr.color = NormalColor; break;
        }
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
