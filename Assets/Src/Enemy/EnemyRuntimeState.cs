using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 戦闘中の敵の状態を保持するシーン常駐コンポーネント。
/// 各部位の現在HPと破壊済みフラグを管理し、ActionCommandPhase/DodgePhase から参照される。
/// SharedHpController と同様、シーンを跨いで生存し続けるため BattleStateMachine の GameObject に配置する。
/// </summary>
public class EnemyRuntimeState : MonoBehaviour
{
    [Header("敵定義")]
    [Tooltip("現在戦っている敵の定義(ScriptableObject)。Step B では1体固定。")]
    public EnemyData enemyData;

    [Header("デバッグ")]
    [Tooltip("Awake で状態をリセットするか。false なら手動 ResetAll で初期化。")]
    public bool resetOnAwake = true;

    private readonly Dictionary<string, int> currentHp = new Dictionary<string, int>();
    private readonly HashSet<string> destroyed = new HashSet<string>();

    /// <summary>
    /// 今ターンに敵が「セリフで予告した部位」。Dodge で予告通りの攻撃が来る想定。
    /// プレイヤーはこの部位を狙って破壊することで予告攻撃を弱化/無効化できる。
    /// PartSelectPhase が <see cref="PickNextTelegraph"/> を毎ターン呼び出してセットする。
    /// </summary>
    public string CurrentTelegraphPartId { get; private set; }
    public EnemyPart CurrentTelegraphPart =>
        (enemyData != null && !string.IsNullOrEmpty(CurrentTelegraphPartId))
            ? enemyData.FindPart(CurrentTelegraphPartId) : null;

    /// <summary>部位HPが変化したときに発火 (part, newHp)。</summary>
    public event Action<EnemyPart, int> OnPartHpChanged;
    /// <summary>部位が破壊された(HP→0)ときに発火。</summary>
    public event Action<EnemyPart> OnPartDestroyed;
    /// <summary>今ターンの予告部位が更新されたときに発火。</summary>
    public event Action<EnemyPart> OnTelegraphChanged;

    void Awake()
    {
        if (resetOnAwake) ResetAll();
    }

    /// <summary>全部位を満HP・未破壊に戻す。</summary>
    public void ResetAll()
    {
        currentHp.Clear();
        destroyed.Clear();
        if (enemyData == null || enemyData.parts == null) return;
        foreach (var p in enemyData.parts)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            currentHp[p.id] = Mathf.Max(0, p.maxHp);
        }
    }

    /// <summary>指定部位にダメージを与える。破壊済み/不明部位は false。</summary>
    public bool ApplyDamage(string partId, int damage)
    {
        if (enemyData == null) return false;
        var part = enemyData.FindPart(partId);
        if (part == null) return false;
        if (destroyed.Contains(partId)) return false;
        int hp = currentHp.TryGetValue(partId, out int h) ? h : part.maxHp;
        hp = Mathf.Max(0, hp - Mathf.Max(0, damage));
        currentHp[partId] = hp;
        OnPartHpChanged?.Invoke(part, hp);
        if (hp <= 0)
        {
            destroyed.Add(partId);
            OnPartDestroyed?.Invoke(part);
            Debug.Log($"[EnemyRuntimeState] 部位破壊: {part.displayName} ({partId}) effect={part.destructionEffect}");
        }
        return true;
    }

    public int GetCurrentHp(string partId) => currentHp.TryGetValue(partId, out int h) ? h : 0;
    public bool IsDestroyed(string partId) => destroyed.Contains(partId);

    /// <summary>
    /// 回復弾(BulletKind.Heal)命中時のボス自己回復。
    /// 破壊されておらず、かつ最大HP未満の部位を1つ探して回復する。
    /// 回復できる部位が無ければ false(=全快 or 全破壊)。
    /// </summary>
    public bool HealAny(int amount)
    {
        if (amount <= 0) return false;
        if (enemyData == null || enemyData.parts == null) return false;
        // 部位破壊で自己回復が封じられている場合は回復しない(回復弾が無効化される)。
        if (HasActiveEffect(PartDestructionEffect.PreventHeal))
        {
            Debug.Log("[EnemyRuntimeState] PreventHeal 発動中のため回復弾を無効化。");
            return false;
        }
        foreach (var p in enemyData.parts)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            if (destroyed.Contains(p.id)) continue;
            int hp = currentHp.TryGetValue(p.id, out int h) ? h : p.maxHp;
            if (hp >= p.maxHp) continue;
            hp = Mathf.Min(p.maxHp, hp + amount);
            currentHp[p.id] = hp;
            OnPartHpChanged?.Invoke(p, hp);
            Debug.Log($"[EnemyRuntimeState] 回復弾でボス回復: {p.displayName} ({p.id}) → HP {hp}/{p.maxHp}");
            return true;
        }
        return false;
    }

    /// <summary>いずれかの破壊済み部位が指定 effect を持っているか。BossDanmakuPattern などが参照。</summary>
    public bool HasActiveEffect(PartDestructionEffect effect)
    {
        if (effect == PartDestructionEffect.None) return false;
        if (enemyData == null || enemyData.parts == null) return false;
        foreach (var p in enemyData.parts)
        {
            if (p == null) continue;
            if (destroyed.Contains(p.id) && p.destructionEffect == effect) return true;
        }
        return false;
    }

    /// <summary>カードの攻撃範囲で狙える(かつ未破壊の)部位を列挙。All の場合は空(=部位選択UIスキップ)。</summary>
    public IEnumerable<EnemyPart> EnumerateSelectableParts(CardAttackRange range)
    {
        if (enemyData == null || enemyData.parts == null) yield break;
        if (range == CardAttackRange.All) yield break; // All は選択UIをスキップ(全部位対象)
        foreach (var p in enemyData.parts)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            if (destroyed.Contains(p.id)) continue;
            if (range == CardAttackRange.Single) { yield return p; continue; }
            if (range == CardAttackRange.Upper && p.zone == BodyZone.Upper) { yield return p; continue; }
            if (range == CardAttackRange.Lower && p.zone == BodyZone.Lower) { yield return p; continue; }
        }
    }

    /// <summary>All 範囲の攻撃用。未破壊部位 ID を列挙。</summary>
    public IEnumerable<string> EnumerateAliveParts()
    {
        if (enemyData == null || enemyData.parts == null) yield break;
        foreach (var p in enemyData.parts)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            if (!destroyed.Contains(p.id)) yield return p.id;
        }
    }

    /// <summary>
    /// 今ターンの「予告部位」を選び直す。生存している部位からランダムに1つ。
    /// 連続で同じ部位を引かないよう、前ターンの予告部位は除外する(候補が1つだけなら例外で許容)。
    /// rng を渡すと再現性確保(null なら新規 System.Random)。
    /// </summary>
    public void PickNextTelegraph(System.Random rng)
    {
        if (enemyData == null || enemyData.parts == null) { CurrentTelegraphPartId = null; return; }
        var alive = new List<EnemyPart>();
        foreach (var p in enemyData.parts)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            if (destroyed.Contains(p.id)) continue;
            alive.Add(p);
        }
        if (alive.Count == 0) { CurrentTelegraphPartId = null; OnTelegraphChanged?.Invoke(null); return; }

        var prev = CurrentTelegraphPartId;
        EnemyPart pick;
        if (alive.Count == 1)
        {
            pick = alive[0];
        }
        else
        {
            var r = rng ?? new System.Random();
            // 前ターンと同じ部位を避ける
            int n = alive.Count;
            int idx = r.Next(n);
            if (!string.IsNullOrEmpty(prev) && alive[idx].id == prev)
                idx = (idx + 1) % n;
            pick = alive[idx];
        }
        CurrentTelegraphPartId = pick.id;
        Debug.Log($"[EnemyRuntimeState] 今ターン予告: {pick.displayName} ({pick.id})");
        OnTelegraphChanged?.Invoke(pick);
    }

    /// <summary>UI に出すための予告セリフを返す。未設定なら空文字。</summary>
    public string GetTelegraphSpeech()
    {
        var p = CurrentTelegraphPart;
        if (p == null) return "";
        return string.IsNullOrWhiteSpace(p.hintCue) ? $"「{p.displayName}でやってやる…!」" : p.hintCue;
    }
}
