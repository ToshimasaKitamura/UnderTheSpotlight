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
                if (t.IsDebugInvincible) continue; // 弾幕デバッグ中は弾をすり抜けさせ、観察を妨げない
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

    // 弾サイズごとの当たり半径(ワールド単位)。丸弾の見た目スケールもこれに比例させる。
    private const float BaseRadius = 0.12f;          // 従来の既定半径(= Medium)
    private static float RadiusOf(BulletSize s)
    {
        switch (s)
        {
            case BulletSize.Small: return 0.075f;
            case BulletSize.Large: return 0.22f;
            default: return BaseRadius;              // Medium
        }
    }

    /// <summary>通常弾(白黒・中弾・丸)を生成する。細かく指定したい場合は overload を使う。</summary>
    public GameObject SpawnBullet(Vector3 pos, Vector3 vel)
    {
        return SpawnBullet(pos, vel, BulletKind.Normal, BulletSize.Medium, BulletShape.Round);
    }

    /// <summary>弾種を指定して弾を生成する(中弾・丸)。</summary>
    public GameObject SpawnBullet(Vector3 pos, Vector3 vel, BulletKind kind)
    {
        return SpawnBullet(pos, vel, kind, BulletSize.Medium, BulletShape.Round);
    }

    /// <summary>弾種・大きさ・形状を指定して弾を生成する。</summary>
    public GameObject SpawnBullet(Vector3 pos, Vector3 vel, BulletKind kind, BulletSize size, BulletShape shape)
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
        b.radius = RadiusOf(size);
        ApplyVisual(go, kind, size, shape, vel);
        bullets.Add(b);
        return go;
    }

    /// <summary>弾種(色)・大きさ・形状(向き)に応じて弾の見た目を設定する。</summary>
    private void ApplyVisual(GameObject bulletGo, BulletKind kind, BulletSize size, BulletShape shape, Vector3 vel)
    {
        var sr = bulletGo.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        switch (kind)
        {
            case BulletKind.Fast: sr.color = FastColor; break;
            case BulletKind.Heal: sr.color = HealColor; break;
            default: sr.color = NormalColor; break;
        }

        // 見た目スケール: 丸は半径に比例、クナイは進行方向へ細長く伸ばして回転。
        float unit = RadiusOf(size) / BaseRadius; // Medium=1
        var t = sr.transform;
        if (shape == BulletShape.Kunai && vel.sqrMagnitude > 0.0001f)
        {
            t.localScale = new Vector3(unit * 2.0f, unit * 0.55f, 1f); // 長辺=進行方向
            float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;      // スプライト長辺(X)を速度へ
            t.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
        else
        {
            t.localScale = new Vector3(unit, unit, 1f);
            t.localRotation = Quaternion.identity;
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
