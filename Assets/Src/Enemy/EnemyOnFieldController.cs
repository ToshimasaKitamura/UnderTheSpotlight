using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyOnFieldController : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    [Tooltip("ボスのワールドサイズ（ユニット単位）")]
    public Vector2 size = new Vector2(2f, 2f);

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetUpSprite(Sprite s)
    {
        // 既存のSpriteRendererを優先して取得
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        // それでもなければ追加
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        spriteRenderer.sprite = s;
        // adjust scale so world size equals size (assuming pixelsPerUnit)
        if (s != null)
        {
            float ppu = s.pixelsPerUnit;
            float worldW = s.rect.width / ppu;
            float scale = size.x / worldW;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
