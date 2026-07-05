using UnityEngine;
using UnityEngine.UI;

public class PulseAlpha : MonoBehaviour
{
    public float minAlpha = 0.05f;
    public float maxAlpha = 0.2f;
    public float speed = 1.0f;
    private Image img;

    void Awake()
    {
        img = GetComponent<Image>();
    }

    void Update()
    {
        if (img == null) return;
        float t = (Mathf.Sin(Time.time * speed * Mathf.PI) + 1f) / 2f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}
