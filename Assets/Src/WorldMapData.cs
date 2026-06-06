using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ワールド1つ分のマップ定義(ScriptableObject)。
/// ノード並び順とマップ表示情報を持つ。
/// Step A MVP: 解放条件は実装しない(全ノード解放扱い)。Step C で prerequisites を有効化。
/// </summary>
[CreateAssetMenu(fileName = "WorldMap", menuName = "UnderTheSpotlight/WorldMapData")]
public class WorldMapData : ScriptableObject
{
    [Header("識別")]
    public string id;
    public string displayName;

    [Header("ステージ並び")]
    [Tooltip("マップ上に並ぶノード。並び順がカーソル移動順になる。")]
    public List<StageEntry> stages = new List<StageEntry>();

    [Header("表示(任意)")]
    [Tooltip("マップ画面の背景画像。")]
    public Sprite mapBackground;

    [Serializable]
    public class StageEntry
    {
        [Tooltip("ステージ本体(StageData)。")]
        public StageData stage;
        [Tooltip("マップ上のノード位置(0..1 で画面の相対位置)。")]
        public Vector2 nodePosition = new Vector2(0.5f, 0.5f);
        [Tooltip("解放条件: ここに並べたステージ ID をクリア済みなら解放。Step A は無視(全解放)。")]
        public List<string> prerequisites = new List<string>();
    }
}
