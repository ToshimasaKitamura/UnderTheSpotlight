using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyOnFieldSpawner : MonoBehaviour
{
    public GameObject enemyOnFieldPrefab; // assign a prefab 256x256
    public Vector3 spawnPosition = new Vector3(0f, 256f, 0f);
    public float appearDelay = 0.1f;

    public IEnumerator Spawn()
    {
        // Wait a moment so this runs after enemy rise
        yield return new WaitForSeconds(appearDelay);

        GameObject go = null;
        if (enemyOnFieldPrefab != null)
        {
            go = Instantiate(enemyOnFieldPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // If no prefab assigned, create a simple placeholder sprite object sized 256x256
            go = new GameObject("EnemyOnField_Auto", typeof(SpriteRenderer));
            go.transform.position = spawnPosition;

            // create a simple white texture sprite 256x256 (pixels-per-unit = 1 so world size = pixels)
            Texture2D tex = new Texture2D(256, 256, TextureFormat.ARGB32, false);
            Color32[] cols = new Color32[256 * 256];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols);
            tex.Apply();

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f), 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = 100; // on top
        }

        // Ensure transform and size: if sprite exists and pixelsPerUnit == 1, scale 1 gives 256x256 world size
        var srenderer = go.GetComponent<SpriteRenderer>();
        if (srenderer != null && srenderer.sprite != null)
        {
            // if sprite pixelsPerUnit is not 1, adjust scale to reach 256 world-units
            float ppu = srenderer.sprite.pixelsPerUnit;
            if (ppu != 1f)
            {
                float worldSize = srenderer.sprite.rect.width / ppu;
                float scale = 256f / worldSize;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                go.transform.localScale = Vector3.one;
            }
        }

        yield break;
    }
}
