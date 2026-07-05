using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ハンマー: ペーパーマリオ風のチャージアタック。
/// SPACEを長押しでパワーをチャージし、最大チャージ時にリリースすると大ダメージ。
/// チャージしすぎると失敗。
/// </summary>
public class HammerMicroGame : IMicroGame
{
    public string DisplayName => "ハンマー!";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private RectTransform chargeBar;
    private RectTransform chargeFill;
    private RectTransform perfectZone;
    private RectTransform overchargeZone;

    private float time, limit;
    private float chargeTime = 0f;
    private float maxChargeTime = 2.0f;
    private float perfectStart = 0.7f;
    private float perfectEnd = 0.85f;
    private float failThreshold = 0.95f;

    private bool isCharging = false;
    private bool hasReleased = false;
    private float barWidth = 800f;

    // PixelFont用
    private MicroGameIMGUIHelper guiHelper;
    private string promptText;

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        chargeTime = 0f;
        isCharging = false;
        hasReleased = false;

        // 難易度調整
        maxChargeTime = Mathf.Max(1.0f, 2.0f - 0.15f * ctx.roundIndex);
        float windowSize = Mathf.Clamp(0.15f * ctx.rangeScale - 0.02f * ctx.roundIndex, 0.08f, 0.2f);
        perfectStart = 0.75f - windowSize / 2f;
        perfectEnd = 0.75f + windowSize / 2f;
        failThreshold = 0.95f;

        root = new GameObject("Hammer", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // IMGUI用ヘルパーを追加
        guiHelper = root.AddComponent<MicroGameIMGUIHelper>();
        guiHelper.Initialize(GetPromptText);
        guiHelper.SetTimer(() => time, () => limit);

        // チャージバー外枠
        var barOuter = AcUi.Image(root.transform, "BarOuter", new Color(0f, 0f, 0f, 1f));
        chargeBar = barOuter.rectTransform;
        AcUi.Place(chargeBar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(barWidth + 16f, 70f));

        var barBg = AcUi.Image(barOuter.transform, "BarBg", new Color(0.12f, 0.13f, 0.2f, 1f));
        AcUi.Stretch(barBg.rectTransform, 6f);

        // パーフェクトゾーン（緑）
        var perfect = AcUi.Image(barBg.transform, "PerfectZone", new Color(0.3f, 0.8f, 0.3f, 0.5f));
        perfectZone = perfect.rectTransform;
        perfectZone.anchorMin = new Vector2(0f, 0.5f);
        perfectZone.anchorMax = new Vector2(0f, 0.5f);
        perfectZone.pivot = new Vector2(0f, 0.5f);
        float perfectX = (barWidth - 4f) * perfectStart + 2f;
        float perfectW = (barWidth - 4f) * (perfectEnd - perfectStart);
        perfectZone.anchoredPosition = new Vector2(perfectX, 0f);
        perfectZone.sizeDelta = new Vector2(perfectW, 52f);

        // オーバーチャージゾーン（赤）
        var overcharge = AcUi.Image(barBg.transform, "OverchargeZone", new Color(0.8f, 0.2f, 0.2f, 0.5f));
        overchargeZone = overcharge.rectTransform;
        overchargeZone.anchorMin = new Vector2(0f, 0.5f);
        overchargeZone.anchorMax = new Vector2(0f, 0.5f);
        overchargeZone.pivot = new Vector2(0f, 0.5f);
        float failX = (barWidth - 4f) * failThreshold + 2f;
        float failW = (barWidth - 4f) * (1f - failThreshold);
        overchargeZone.anchoredPosition = new Vector2(failX, 0f);
        overchargeZone.sizeDelta = new Vector2(failW, 52f);

        // チャージフィル（黄色→オレンジ）
        var fill = AcUi.Image(barBg.transform, "Fill", ctx.accent);
        chargeFill = fill.rectTransform;
        chargeFill.anchorMin = new Vector2(0f, 0.5f);
        chargeFill.anchorMax = new Vector2(0f, 0.5f);
        chargeFill.pivot = new Vector2(0f, 0.5f);
        chargeFill.anchoredPosition = new Vector2(2f, 0f);
        chargeFill.sizeDelta = new Vector2(0f, 52f);

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (!isCharging && !hasReleased)
        {
            promptText = "HOLD SPACE!";
        }
        else if (isCharging)
        {
            float progress = chargeTime / maxChargeTime;
            if (progress >= failThreshold)
            {
                promptText = "TOO MUCH!";
            }
            else if (progress >= perfectStart && progress <= perfectEnd)
            {
                promptText = "PERFECT! RELEASE!";
            }
            else
            {
                promptText = "CHARGING...";
            }
        }
        else
        {
            promptText = "";
        }
    }

    public string GetPromptText() => promptText;

    public bool Tick(float dt)
    {
        time += dt;

        var kb = Keyboard.current;
        if (kb != null)
        {
            bool spacePressed = kb.spaceKey.isPressed;
            bool spaceReleased = kb.spaceKey.wasReleasedThisFrame;

            if (!hasReleased)
            {
                if (spacePressed)
                {
                    isCharging = true;
                    chargeTime += dt;
                    chargeTime = Mathf.Min(chargeTime, maxChargeTime);

                    // チャージバー更新
                    float progress = chargeTime / maxChargeTime;
                    chargeFill.sizeDelta = new Vector2((barWidth - 4f) * progress, 52f);

                    // オーバーチャージで失敗
                    if (progress >= 1f)
                    {
                        Succeeded = false;
                        hasReleased = true;
                        UpdatePrompt();
                        return true;
                    }
                }

                if (spaceReleased && isCharging)
                {
                    hasReleased = true;
                    float progress = chargeTime / maxChargeTime;

                    // パーフェクトゾーンで成功
                    if (progress >= perfectStart && progress <= perfectEnd)
                    {
                        Succeeded = true;
                    }
                    else if (progress >= failThreshold)
                    {
                        Succeeded = false;
                    }
                    else
                    {
                        // 早すぎても成功だが低ダメージ扱い
                        Succeeded = progress >= 0.5f;
                    }

                    UpdatePrompt();
                    return true;
                }
            }

            UpdatePrompt();
        }

        // タイムアップ
        if (time >= limit)
        {
            Succeeded = false;
            return true;
        }

        return false;
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
