using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CardSelect の選択結果からミニゲームの実効ノブを算出する(雛形)。
/// 現段階: 選択カードの実効難度合計で初期難易度をバイアス + ラウンド毎 ramp。
/// CardEffectType ごとの個別マッピング(個人/全体デメリット)は枠のみ用意し、
/// 数値は ActionCommandPhase の Inspector で調整できる係数を使う。
/// </summary>
public class ActionCommandModifiers
{
    public struct Knobs
    {
        public float timeLimit;
        public float speedMul;
        public int buttonCount;
        public float rangeScale;
        public float interference;
    }

    public struct Tuning
    {
        public float baseTimeLimit;
        public float timeLimitPerRound;   // ラウンド毎に短縮(負)
        public float speedRampPerRound;   // ラウンド毎に加算
        public int buttonCountBase;
        public float difficultyToSpeed;   // カード難度合計1点あたり speed 加算
        public float difficultyToInterfere; // 同 interference 加算
        public float minTimeLimit;
    }

    private readonly int totalCardDifficulty;

    public ActionCommandModifiers(IReadOnlyList<CardInstance> selections)
    {
        totalCardDifficulty = 0;
        if (selections != null)
        {
            foreach (var c in selections)
            {
                if (c != null) totalCardDifficulty += Mathf.Max(0, c.EffectiveDifficulty);
            }
        }
    }

    public int TotalCardDifficulty => totalCardDifficulty;

    /// <summary>ラウンド round の実効ノブを算出。</summary>
    public Knobs Resolve(int round, Tuning t)
    {
        float diffBias = totalCardDifficulty;
        var k = new Knobs
        {
            timeLimit = Mathf.Max(t.minTimeLimit,
                          t.baseTimeLimit + t.timeLimitPerRound * round),
            speedMul = 1f + t.speedRampPerRound * round + t.difficultyToSpeed * diffBias,
            buttonCount = Mathf.Max(2, t.buttonCountBase + round),
            rangeScale = Mathf.Clamp(1f - 0.04f * round, 0.5f, 1.2f),
            interference = Mathf.Clamp01(t.difficultyToInterfere * diffBias + 0.05f * round),
        };
        return k;
    }
}
