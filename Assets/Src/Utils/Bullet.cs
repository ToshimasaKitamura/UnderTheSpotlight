using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Vector3 velocity;
    public float radius = 0.12f;   // ワールド単位(弾の当たり半径)
    void Update()
    {
        transform.position += velocity * Time.deltaTime;
    }
}
