using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ステージ1つ分の定義(ScriptableObject)。
/// 中で複数のボス連戦を持つ。クリア報酬は経験値とボム。
/// Step A MVP: 連戦・報酬・解放条件はデータ保持のみ(実適用は Step B 以降)。
/// </summary>
[CreateAssetMenu(fileName = "Stage", menuName = "UnderTheSpotlight/StageData")]
public class StageData : ScriptableObject
{
    [Header("識別")]
    [Tooltip("内部識別子。CSV/セーブから参照。")]
    public string id;
    [Tooltip("プレイヤー向け表示名。例: 「森の入口」")]
    public string displayName;

    [Header("ボス連戦")]
    [Tooltip("このステージで順番に登場するボスたち。複数で連戦。")]
    public List<EnemyData> bosses = new List<EnemyData>();

    [Header("報酬(MVP は数値保持のみ)")]
    [Tooltip("ボス1体撃破ごとに得られる基本経験値。")]
    public int baseExpReward = 5;
    [Tooltip("ボス1体撃破ごとに得られる基本ボム数。")]
    public int baseBombReward = 1;
    [Tooltip("ステージ全撃破時の追加経験値。")]
    public int clearBonusExp = 10;
    [Tooltip("ステージ全撃破時の追加ボム。")]
    public int clearBonusBombs = 0;

    [Header("表示(任意)")]
    [Tooltip("マップ画面上でこのステージを示すアイコン(任意、空でも可)。")]
    public Sprite nodeIcon;
    [Tooltip("ステージ突入時の背景(任意)。")]
    public Sprite environmentBackground;

    /// <summary>連戦のボス数。0 ならステージ無効。</summary>
    public int BossCount => bosses != null ? bosses.Count : 0;
}
