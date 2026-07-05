using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1ラン(マップ周回1回分)の進捗を保持するシーン常駐コンポーネント。
/// SharedHpController と同じく BattleStateMachine GameObject 等にぶら下げる。
/// Step A MVP: 現在ステージ・連戦の何体目か・取得経験値/ボムの累積のみ。
/// セーブは Step C で実装。
/// </summary>
public class RunProgress : MonoBehaviour
{
    [Header("ワールド")]
    [Tooltip("使用する WorldMapData。Inspector で指定。")]
    public WorldMapData worldMap;

    [Header("現在の進行(ランタイム / Inspector でデバッグ用に上書き可)")]
    [Tooltip("プレイ中のステージ(マップで選ばれたもの)。null ならマップ画面状態。")]
    public StageData currentStage;
    [Tooltip("連戦の何体目か(0始まり)。1ステージ複数ボス連戦に対応。")]
    public int currentBossIndex;

    [Header("累積")]
    public int accumulatedExp;
    public int accumulatedBombs;

    [Header("クリア記録")]
    [Tooltip("これまでにクリアした StageData.id の集合。Step A は無使用、Step C 以降で解放判定に。")]
    public List<string> clearedStageIds = new List<string>();

    [Header("ステージ通しの条件チェック")]
    public bool noDamageThisStage = true;
    public int bombsUsedThisStage = 0;

    /// <summary>ステージ突入時のリセット。連戦開始 / マップから戻る毎に呼ぶ。</summary>
    public event Action<StageData> OnStageBegin;
    /// <summary>連戦内の次のボスへ移る時(2体目以降の戦闘開始)。</summary>
    public event Action<StageData, int> OnNextBoss;
    /// <summary>ステージクリア時(報酬集計後)。</summary>
    public event Action<StageData> OnStageCleared;

    /// <summary>マップ画面で選んだステージで突入する。</summary>
    public void BeginStage(StageData stage)
    {
        currentStage = stage;
        currentBossIndex = 0;
        noDamageThisStage = true;
        bombsUsedThisStage = 0;
        OnStageBegin?.Invoke(stage);
    }

    /// <summary>1体撃破した直後の処理。次のボスに移るか、ステージクリアか。</summary>
    public void OnBossDefeated()
    {
        if (currentStage == null) return;
        accumulatedExp += currentStage.baseExpReward;
        accumulatedBombs += currentStage.baseBombReward;
        currentBossIndex++;
        if (currentBossIndex >= currentStage.BossCount)
        {
            // 全撃破 → クリアボーナス
            accumulatedExp += currentStage.clearBonusExp;
            accumulatedBombs += currentStage.clearBonusBombs;
            if (!clearedStageIds.Contains(currentStage.id))
                clearedStageIds.Add(currentStage.id);
            OnStageCleared?.Invoke(currentStage);
        }
        else
        {
            OnNextBoss?.Invoke(currentStage, currentBossIndex);
        }
    }

    /// <summary>マップに復帰する(ステージ離脱 or クリア後)。</summary>
    public void EndStage()
    {
        currentStage = null;
        currentBossIndex = 0;
    }

    /// <summary>現在連戦中の今のボス(EnemyData)。null ならステージ非選択。</summary>
    public EnemyData CurrentBoss
    {
        get
        {
            if (currentStage == null) return null;
            if (currentStage.bosses == null || currentStage.bosses.Count == 0) return null;
            int i = Mathf.Clamp(currentBossIndex, 0, currentStage.bosses.Count - 1);
            return currentStage.bosses[i];
        }
    }

    /// <summary>Step A デバッグ: クリア記録に関係なくノードは全解放扱い。</summary>
    public bool IsStageUnlocked(WorldMapData.StageEntry entry)
    {
        // Step C で prerequisites 評価。MVP は常に true。
        return true;
    }
}
