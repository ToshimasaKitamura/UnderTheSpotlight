using UnityEngine;

/// <summary>
/// 弾の種別。難易度と用途で弾を撃ち分ける。
/// 基本は白黒(Normal)。用途で見た目と挙動を変える(仕様: docs/Design/Danmaku_Design.md)。
/// </summary>
public enum BulletKind
{
    /// <summary>通常弾。白黒の素直な弾。</summary>
    Normal = 0,
    /// <summary>速弾(クナイ的)。鋭い直線弾。</summary>
    Fast = 1,
    /// <summary>回復弾(緑)。自機に命中するとボスが自己回復する(避けるべき弾)。</summary>
    Heal = 2,
}

public class Bullet : MonoBehaviour
{
    public Vector3 velocity;
    public float radius = 0.12f;   // ワールド単位(弾の当たり半径)

    [Tooltip("弾種。DanmakuManager が生成時に設定する。")]
    public BulletKind kind = BulletKind.Normal;
    [Tooltip("Heal 弾が自機に命中したときにボスが回復する量(部位HP)。")]
    public int healAmount = 1;

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
    }
}
