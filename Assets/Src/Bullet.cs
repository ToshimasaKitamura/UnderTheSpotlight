using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Vector3 velocity;
    public float radius = 4f;
    void Update()
    {
        transform.position += velocity * Time.deltaTime;
    }
}
