/// <summary>
/// カードのカテゴリ(行動種別)。5種。ActionCommand のミニゲーム選択や効果分岐に使う。
/// </summary>
public enum CardCategory
{
    Attack,
    Defend,
    Heal,
    Buff,
    Special,
}

/// <summary>
/// カードのレアリティ階層。デッキ構成比(基本/特殊/必殺)を表す。
/// </summary>
public enum CardTier
{
    Basic,    // 基本
    Special,  // 特殊
    Ultimate, // 必殺
}

/// <summary>
/// 戦闘で適用される効果ロジックの種別。
/// 現段階は「データとして保持」するのみで、実際の戦闘への適用は後段で実装する。
/// 新しい効果を追加する場合はここに enum 値を足し、対応ハンドラを別途用意する。
/// </summary>
public enum CardEffectType
{
    None,
    /// 一度も被弾しなかった場合、攻撃終了後に大ダメージ
    AttackNoHitBonus,
    /// 敵の攻撃対象に選ばれやすくなり、被ダメージ軽減
    DefendTauntReduce,
    /// 即時HP小回復 + ギリギリ回避ごとに小回復
    HealImmediateAndOnDodge,
    /// 汎用バフ(攻撃力/防御力等の一時上昇)
    BuffGeneric,
    /// アクションコマンドの成功数が多いほど被ダメージ軽減
    SpecialSuccessScaling,
}

/// <summary>
/// カードの攻撃範囲。部位破壊システムで、どの部位を狙えるかを決める。
/// Single  = 単体(任意の1部位を選択)
/// Upper   = 上半身(頭・腕など 上半身に属する部位のみ)
/// Lower   = 下半身(足など 下半身に属する部位のみ)
/// All     = 全体(全部位に同時にダメージ。部位選択UIをスキップ)
/// 非攻撃カード(防御/回復/バフ/特殊)は All を既定値として扱う。
/// </summary>
public enum CardAttackRange
{
    Single = 0,
    Upper = 1,
    Lower = 2,
    All = 3,
}

/// <summary>
/// 敵の部位を破壊したときに発動する効果。Step A ではデータとして保持するのみ。
/// 戦闘ロジック側(BossDanmakuPattern / EnemyOnFieldController 等)への適用は Step B で実装。
/// </summary>
public enum PartDestructionEffect
{
    None = 0,
    /// 敵の次の攻撃を弱化(密度・速度を一時的に下げる)
    WeakenNextAttack = 1,
    /// 回復不可(敵側の自己回復スキルを封じる)
    PreventHeal = 2,
    /// 弾幕の密度(弾数)を恒常的に下げる
    ReducePatternDensity = 3,
    /// 弾幕の弾速を恒常的に下げる
    ReducePatternSpeed = 4,
    /// 敵の移動(横移動・ジャンプ等)を停止
    DisableMovement = 5,
}

/// <summary>
/// 部位が属する身体部位(攻撃範囲フィルタ用)。
/// CardAttackRange.Upper は Upper 部位のみ、Lower は Lower 部位のみを狙える。
/// </summary>
public enum BodyZone
{
    Upper = 0,
    Lower = 1,
}
