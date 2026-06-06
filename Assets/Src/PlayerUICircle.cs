using UnityEngine;
using UnityEngine.UI;

public class PlayerUICircle : MonoBehaviour
{
    public RectTransform rect;
    public Image image;
    public string playerId;
    public float radius = 25f; // half of 50

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }

    public void Init(Color col, string id)
    {
        if (image != null) image.color = col;
        playerId = id;
    }

    public void OnHit()
    {
        Debug.Log($"[PlayerUICircle] Player {playerId} hit!");
        // simple visual on hit
        if (image != null) image.color = Color.black;
    }
}
