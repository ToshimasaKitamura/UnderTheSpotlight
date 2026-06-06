using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>ホールド: 往復する針がターゲット帯にある間に SPACE を離す。</summary>
public class HoldMicroGame : IMicroGame
{
    public string DisplayName => "ぴたっとリリース！";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private RectTransform needle;
    private Text prompt;
    private float time, limit;
    private float trackW = 880f;
    private float pos, dir = 1f, speed;
    private float zoneHalf;
    private bool wasHeld;

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        pos = -1f; dir = 1f;
        speed = (1.0f + 0.3f * ctx.roundIndex) * ctx.speedMul;
        zoneHalf = Mathf.Clamp(0.14f * ctx.rangeScale - 0.012f * ctx.roundIndex, 0.05f, 0.32f);
        wasHeld = false;

        root = new GameObject("Hold", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        prompt = AcUi.Label(root.transform, "P", 56, Color.white, TextAnchor.MiddleCenter, true, true);
        AcUi.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1300f, 90f));
        prompt.text = "SPACE 長押し → 帯で離す！";

        var outer = AcUi.Image(root.transform, "Track", new Color(0f, 0f, 0f, 1f));
        AcUi.Place(outer.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(trackW + 16f, 80f));
        var bg = AcUi.Image(outer.transform, "Bg", new Color(0.12f, 0.13f, 0.2f, 1f));
        AcUi.Stretch(bg.rectTransform, 6f);
        var zone = AcUi.Image(bg.transform, "Zone", new Color(ctx.accent.r, ctx.accent.g, ctx.accent.b, 0.55f));
        AcUi.Place(zone.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(trackW * zoneHalf * 2f, 60f));
        var nd = AcUi.Image(bg.transform, "Needle", Color.white);
        needle = nd.rectTransform;
        needle.anchorMin = new Vector2(0.5f, 0.5f);
        needle.anchorMax = new Vector2(0.5f, 0.5f);
        needle.pivot = new Vector2(0.5f, 0.5f);
        needle.sizeDelta = new Vector2(12f, 76f);
    }

    public bool Tick(float dt)
    {
        time += dt;
        pos += dir * speed * dt;
        if (pos > 1f) { pos = 1f; dir = -1f; }
        else if (pos < -1f) { pos = -1f; dir = 1f; }
        needle.anchoredPosition = new Vector2(pos * (trackW * 0.5f - 8f), 0f);

        var kb = Keyboard.current;
        bool held = kb != null && kb.spaceKey.isPressed;
        if (wasHeld && !held) // リリース
        {
            Succeeded = Mathf.Abs(pos) <= zoneHalf * 2f;
            return true;
        }
        wasHeld = held;
        if (time >= limit) { Succeeded = false; return true; }
        return false;
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
