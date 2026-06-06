using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>タイミング: 往復するマーカーがターゲット帯に重なった瞬間に SPACE。</summary>
public class TimingMicroGame : IMicroGame
{
    public string DisplayName => "ジャストタイミング！";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private RectTransform marker;
    private Text prompt;
    private float time, limit;
    private float trackW = 900f;
    private float pos;     // -1..1
    private float dir = 1f;
    private float speed;
    private float zoneHalf; // 正規化(0..1)での半幅
    private int needed;
    private int hits;

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        pos = -1f;
        dir = 1f;
        speed = (1.1f + 0.35f * ctx.roundIndex) * ctx.speedMul;
        zoneHalf = Mathf.Clamp(0.12f * ctx.rangeScale - 0.012f * ctx.roundIndex, 0.04f, 0.3f);
        needed = Mathf.Max(1, ctx.buttonCount - 1);
        hits = 0;

        root = new GameObject("Timing", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        prompt = AcUi.Label(root.transform, "P", 60, Color.white, TextAnchor.MiddleCenter, true, true);
        AcUi.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 90f));

        var outer = AcUi.Image(root.transform, "Track", new Color(0f, 0f, 0f, 1f));
        AcUi.Place(outer.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(trackW + 16f, 80f));
        var bg = AcUi.Image(outer.transform, "Bg", new Color(0.12f, 0.13f, 0.2f, 1f));
        AcUi.Stretch(bg.rectTransform, 6f);

        var zone = AcUi.Image(bg.transform, "Zone", new Color(ctx.accent.r, ctx.accent.g, ctx.accent.b, 0.55f));
        AcUi.Place(zone.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(trackW * zoneHalf * 2f, 60f));

        var mk = AcUi.Image(bg.transform, "Marker", Color.white);
        marker = mk.rectTransform;
        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(14f, 76f);

        UpdatePrompt();
    }

    private void UpdatePrompt() => prompt.text = $"重なった瞬間に SPACE！  {hits}/{needed}";

    public bool Tick(float dt)
    {
        time += dt;
        pos += dir * speed * dt;
        if (pos > 1f) { pos = 1f; dir = -1f; }
        else if (pos < -1f) { pos = -1f; dir = 1f; }
        marker.anchoredPosition = new Vector2(pos * (trackW * 0.5f - 8f), 0f);

        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            if (Mathf.Abs(pos) <= zoneHalf * 2f)
            {
                hits++;
                UpdatePrompt();
                if (hits >= needed) { Succeeded = true; return true; }
            }
            else { Succeeded = false; return true; } // 外したら失敗
        }
        if (time >= limit) { Succeeded = false; return true; }
        return false;
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
