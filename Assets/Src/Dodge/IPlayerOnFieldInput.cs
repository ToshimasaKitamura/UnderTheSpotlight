using UnityEngine;

/// <summary>
/// 弾幕フィールド上の自機の入力ソース抽象。
/// キーボード/ゲームパッド/AI 等の実装が ReadMovement で2軸の移動入力を提供する。
/// </summary>
public interface IPlayerOnFieldInput
{
    /// <summary>
    /// 移動入力 (-1..1) の2軸ベクトルを返す。デバイス未接続時は Vector2.zero。
    /// </summary>
    Vector2 ReadMovement();
}
