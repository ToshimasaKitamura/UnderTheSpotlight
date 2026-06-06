using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵キャラ1体の定義(ScriptableObject)。
/// 部位破壊システムの基本データ単位。1体の敵は複数の <see cref="EnemyPart"/> を持ち、
/// それぞれを個別に破壊することで <see cref="PartDestructionEffect"/> が発動する。
/// Project ウィンドウから Create > UnderTheSpotlight > EnemyData で作成可能。
///
/// Step A(現段階)はデータ保持のみ。
/// UI・戦闘への適用は Step B 以降。
/// </summary>
[CreateAssetMenu(fileName = "Enemy", menuName = "UnderTheSpotlight/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("識別")]
    [Tooltip("内部識別子(CSV から参照)。例: hamster")]
    public string id;
    [Tooltip("プレイヤー向け表示名。例: 「ハムスター」")]
    public string displayName;

    [Header("部位定義")]
    [Tooltip("この敵が持つ部位の一覧。攻撃カードの 部位選択UI に並び、それぞれ独立した HP を持つ。")]
    public List<EnemyPart> parts = new List<EnemyPart>();

    [Header("表示(任意)")]
    [Tooltip("敵アイコン(EnemyOnField 表示用、空でも可)。")]
    public Sprite icon;

    /// <summary>id で部位を引く。見つからなければ null。</summary>
    public EnemyPart FindPart(string partId)
    {
        if (parts == null || string.IsNullOrEmpty(partId)) return null;
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != null && parts[i].id == partId) return parts[i];
        }
        return null;
    }
}

/// <summary>
/// 敵の1部位の定義。EnemyData に内包される。
/// CSV では (enemyId, partId) の複合キーで識別する。
/// </summary>
[Serializable]
public class EnemyPart
{
    [Tooltip("部位識別子(敵内でユニーク)。例: head, arms, legs")]
    public string id;
    [Tooltip("表示名。例: 「頭」「腕」「足」")]
    public string displayName;
    [Tooltip("部位の最大HP。攻撃カード命中でこの値ぶん減らし、0で破壊。")]
    public int maxHp = 3;
    [Tooltip("攻撃範囲フィルタ用。Upper=上半身, Lower=下半身。")]
    public BodyZone zone = BodyZone.Upper;
    [Tooltip("破壊時に発動する効果種別。")]
    public PartDestructionEffect destructionEffect = PartDestructionEffect.None;
    [TextArea(1, 3)]
    [Tooltip("この部位を狙わせるためのヒント(セリフ/動きの説明)。Dodge 中や PartSelect UI で表示する想定。")]
    public string hintCue;

    [Header("ヒットボックス(敵スプライト上の重ね矩形 / 正規化 0..1)")]
    [Tooltip("矩形の中心座標(0..1)。0,0 が左下、1,1 が右上。")]
    public Vector2 hitboxCenter = new Vector2(0.5f, 0.5f);
    [Tooltip("矩形のサイズ(0..1)。スプライト矩形に対する比率。")]
    public Vector2 hitboxSize = new Vector2(0.4f, 0.3f);
}
