using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カード効果(攻撃以外含む)とバトル中のバフ/デバフを管理するシステム。
/// ActionCommandPhase, BattleResultPhase, DodgePhase から参照される。
/// </summary>
public class BattleEffectProcessor : MonoBehaviour
{
    [Header("依存")]
    [SerializeField] private SharedHpController sharedHpController;
    [SerializeField] private EnemyRuntimeState enemyState;
    [SerializeField] private GameTuning tuning;

    /// <summary>現在有効なバフ/デバフのリスト</summary>
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    /// <summary>今ターンのドッジフェーズで被弾したか</summary>
    public bool WasHitThisTurn { get; set; }

    /// <summary>今ターンのギリギリ回避回数</summary>
    public int GrazeCountThisTurn { get; set; }

    /// <summary>バフが追加・削除されたときに発火</summary>
    public event Action OnBuffsChanged;

    private static BattleEffectProcessor instance;
    public static BattleEffectProcessor Instance => instance;

    /// <summary>バフの種類</summary>
    public enum BuffType
    {
        AttackUp,       // 攻撃力上昇
        DefenseUp,      // 防御力上昇(被ダメージ軽減)
        SpeedUp,        // 移動速度上昇
        DamageReduction,// ダメージ軽減(固定値)
        HealOverTime,   // 継続回復
        Taunt,          // 挑発(被弾時ダメージ軽減)
    }

    public class ActiveBuff
    {
        public BuffType type;
        public int value;           // 効果値
        public int turnsRemaining;  // 残りターン数(0=永続)
        public int sourcePlayerIndex;
    }

    void Awake()
    {
        instance = this;
        if (sharedHpController == null) sharedHpController = FindObjectOfType<SharedHpController>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>ターン開始時にリセット</summary>
    public void OnTurnStart()
    {
        WasHitThisTurn = false;
        GrazeCountThisTurn = 0;

        // バフのターン経過処理
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = activeBuffs[i];
            if (buff.turnsRemaining > 0)
            {
                buff.turnsRemaining--;
                if (buff.turnsRemaining <= 0)
                {
                    activeBuffs.RemoveAt(i);
                }
            }
        }
        OnBuffsChanged?.Invoke();
    }

    /// <summary>カード効果を処理する(アクションコマンド成功時に呼ばれる)</summary>
    public void ProcessCardEffect(int playerIndex, CardInstance card, int successCount)
    {
        if (card == null || card.Data == null || successCount <= 0) return;

        switch (card.Category)
        {
            case CardCategory.Heal:
                ProcessHeal(playerIndex, card, successCount);
                break;
            case CardCategory.Defend:
                ProcessDefend(playerIndex, card, successCount);
                break;
            case CardCategory.Buff:
                ProcessBuff(playerIndex, card, successCount);
                break;
            case CardCategory.Special:
                ProcessSpecial(playerIndex, card, successCount);
                break;
            // Attackは既存のHandlePlayerAttackで処理
        }

        // カード固有効果
        ProcessCardEffectType(playerIndex, card, successCount);
    }

    private void ProcessHeal(int playerIndex, CardInstance card, int successCount)
    {
        if (sharedHpController == null) return;

        int healAmount = card.EffectivePower * successCount;
        int currentHp = sharedHpController.CurrentHp;

        sharedHpController.Heal(healAmount);

        int actualHeal = sharedHpController.CurrentHp - currentHp;
        if (actualHeal > 0)
        {
            Debug.Log($"[BattleEffectProcessor] P{playerIndex + 1} 回復 +{actualHeal} (HP: {currentHp} → {sharedHpController.CurrentHp})");
        }
    }

    private void ProcessDefend(int playerIndex, CardInstance card, int successCount)
    {
        // 防御バフを付与
        int defenseValue = card.EffectivePower * successCount;
        AddBuff(new ActiveBuff
        {
            type = BuffType.DefenseUp,
            value = defenseValue,
            turnsRemaining = 1, // 次のドッジフェーズのみ有効
            sourcePlayerIndex = playerIndex
        });

        Debug.Log($"[BattleEffectProcessor] P{playerIndex + 1} 防御バフ +{defenseValue}");
    }

    private void ProcessBuff(int playerIndex, CardInstance card, int successCount)
    {
        // 汎用バフを付与
        int buffValue = card.EffectivePower * successCount;
        AddBuff(new ActiveBuff
        {
            type = BuffType.AttackUp,
            value = buffValue,
            turnsRemaining = 2,
            sourcePlayerIndex = playerIndex
        });

        Debug.Log($"[BattleEffectProcessor] P{playerIndex + 1} 攻撃バフ +{buffValue} (2ターン)");
    }

    private void ProcessSpecial(int playerIndex, CardInstance card, int successCount)
    {
        // 特殊カードの効果は CardEffectType に基づいて処理
        // ここでは追加のバフなどを付与可能
        Debug.Log($"[BattleEffectProcessor] P{playerIndex + 1} 特殊カード効果発動");
    }

    private void ProcessCardEffectType(int playerIndex, CardInstance card, int successCount)
    {
        switch (card.EffectType)
        {
            case CardEffectType.AttackNoHitBonus:
                // ドッジフェーズ終了時にWasHitThisTurnをチェックして追加ダメージ
                // (実際の適用はDodgePhase終了時)
                Debug.Log($"[BattleEffectProcessor] P{playerIndex + 1} ノーヒットボーナス効果待機中");
                break;

            case CardEffectType.DefendTauntReduce:
                AddBuff(new ActiveBuff
                {
                    type = BuffType.Taunt,
                    value = card.EffectivePower,
                    turnsRemaining = 1,
                    sourcePlayerIndex = playerIndex
                });
                break;

            case CardEffectType.HealImmediateAndOnDodge:
                // 即時回復
                ProcessHeal(playerIndex, card, 1);
                // ギリギリ回避ごとの回復は GrazeCountThisTurn を参照
                break;

            case CardEffectType.BuffGeneric:
                // 攻撃・防御両方上昇
                AddBuff(new ActiveBuff
                {
                    type = BuffType.AttackUp,
                    value = card.EffectivePower,
                    turnsRemaining = 2,
                    sourcePlayerIndex = playerIndex
                });
                AddBuff(new ActiveBuff
                {
                    type = BuffType.DefenseUp,
                    value = card.EffectivePower,
                    turnsRemaining = 2,
                    sourcePlayerIndex = playerIndex
                });
                break;

            case CardEffectType.SpecialSuccessScaling:
                // アクションコマンド成功数に応じたダメージ軽減
                AddBuff(new ActiveBuff
                {
                    type = BuffType.DamageReduction,
                    value = successCount, // 成功数 = 軽減量
                    turnsRemaining = 1,
                    sourcePlayerIndex = playerIndex
                });
                break;
        }
    }

    /// <summary>バフを追加</summary>
    public void AddBuff(ActiveBuff buff)
    {
        activeBuffs.Add(buff);
        OnBuffsChanged?.Invoke();
    }

    /// <summary>指定タイプのバフの合計値を取得</summary>
    public int GetBuffValue(BuffType type)
    {
        int total = 0;
        foreach (var buff in activeBuffs)
        {
            if (buff.type == type)
                total += buff.value;
        }
        return total;
    }

    /// <summary>全バフをクリア</summary>
    public void ClearAllBuffs()
    {
        activeBuffs.Clear();
        OnBuffsChanged?.Invoke();
    }

    /// <summary>現在有効なバフリスト</summary>
    public IReadOnlyList<ActiveBuff> GetActiveBuffs() => activeBuffs;

    /// <summary>ドッジフェーズ終了時に呼ばれる(ノーヒットボーナス等の処理)</summary>
    public void OnDodgePhaseEnd(IReadOnlyList<CardInstance> selections, IReadOnlyList<int> perPlayerSuccess)
    {
        if (selections == null || enemyState == null) return;

        for (int i = 0; i < selections.Count; i++)
        {
            var card = selections[i];
            if (card == null) continue;

            int successCount = (perPlayerSuccess != null && i < perPlayerSuccess.Count) ? perPlayerSuccess[i] : 0;
            if (successCount <= 0) continue;

            // ノーヒットボーナス
            if (card.EffectType == CardEffectType.AttackNoHitBonus && !WasHitThisTurn)
            {
                int bonusDamage = card.EffectivePower * 2; // 2倍ダメージ
                Debug.Log($"[BattleEffectProcessor] P{i + 1} ノーヒットボーナス発動! 追加ダメージ {bonusDamage}");

                // 全部位にダメージ
                foreach (var partId in enemyState.EnumerateAliveParts())
                {
                    enemyState.ApplyDamage(partId, bonusDamage);
                }
            }

            // ギリギリ回避回復
            if (card.EffectType == CardEffectType.HealImmediateAndOnDodge && GrazeCountThisTurn > 0)
            {
                int grazeHeal = GrazeCountThisTurn;
                Debug.Log($"[BattleEffectProcessor] P{i + 1} ギリギリ回避回復 +{grazeHeal}");
                // 回復処理
            }
        }

        // ターン終了時のバフ消費
        OnTurnStart();
    }

    /// <summary>被ダメージを計算(防御バフを考慮)</summary>
    public int CalculateDamageTaken(int baseDamage)
    {
        int defense = GetBuffValue(BuffType.DefenseUp);
        int reduction = GetBuffValue(BuffType.DamageReduction);
        int taunt = GetBuffValue(BuffType.Taunt);

        int finalDamage = baseDamage - defense - reduction;
        if (taunt > 0)
        {
            finalDamage = Mathf.Max(1, finalDamage / 2); // 挑発時は半減
        }

        return Mathf.Max(0, finalDamage);
    }

    /// <summary>攻撃力計算(バフを考慮)</summary>
    public int CalculateAttackPower(int basePower)
    {
        int attackUp = GetBuffValue(BuffType.AttackUp);
        return basePower + attackUp;
    }
}
