using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>連打: 制限時間内に SPACE を連打してゲージを満たす。</summary>
public class MashMicroGame : IMicroGame
{
    public string DisplayName => "れんだ！";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private RectTransform fillRt;
    private float time;
    private float limit;
    private float value;     // 0..1
    private float required;  // 到達目標(常に1で表現, decay で難化)
    private float decayPerSec;
    private float gainPerPress;
    private float barW;

    // PixelFont用
    private MicroGameIMGUIHelper guiHelper;
    private const string promptText = "SPACE!";

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        value = 0f;
        required = 1f;
        // 難化: speed/round で decay 増・1押し増分減
        decayPerSec = 0.25f + 0.15f * ctx.roundIndex * ctx.speedMul;  // 減衰速度増加
        gainPerPress = Mathf.Max(0.030f, 0.070f / Mathf.Max(0.5f, ctx.speedMul));  // 1回の増加量減少
        barW = 760f;

        root = new GameObject("Mash", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // IMGUI用ヘルパーを追加
        guiHelper = root.AddComponent<MicroGameIMGUIHelper>();
        guiHelper.Initialize(GetPromptText);
        guiHelper.SetTimer(() => time, () => limit);

        var outer = AcUi.Image(root.transform, "BarOuter", new Color(0f, 0f, 0f, 1f));
        AcUi.Place(outer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(barW + 16f, 70f));
        var bg = AcUi.Image(outer.transform, "BarBg", new Color(0.12f, 0.13f, 0.2f, 1f));
        AcUi.Stretch(bg.rectTransform, 6f);
        var fill = AcUi.Image(bg.transform, "Fill", ctx.accent);
        fillRt = fill.rectTransform;
        fillRt.anchorMin = new Vector2(0f, 0.5f);
        fillRt.anchorMax = new Vector2(0f, 0.5f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = new Vector2(2f, 0f);
        fillRt.sizeDelta = new Vector2(0f, 52f);
    }

    public string GetPromptText() => promptText;

    public bool Tick(float dt)
    {
        time += dt;
        value -= decayPerSec * dt;
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) value += gainPerPress;
        value = Mathf.Clamp01(value);
        fillRt.sizeDelta = new Vector2((barW - 4f) * value, 52f);

        if (value >= required) { Succeeded = true; return true; }
        if (time >= limit) { Succeeded = false; return true; }
        return false;
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
