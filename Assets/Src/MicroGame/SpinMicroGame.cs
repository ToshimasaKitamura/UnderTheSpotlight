using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// スピン: ペーパーマリオ風の回転攻撃。
/// 矢印キーを時計回り/反時計回りに素早く入力してパワーを溜める。
/// ゲージを満タンにすれば成功。
/// </summary>
public class SpinMicroGame : IMicroGame
{
    public string DisplayName => "スピン!";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private RectTransform powerBar;
    private RectTransform powerFill;

    private float time, limit;
    private float power = 0f;
    private float powerRequired = 1f;
    private float powerDecay = 0.3f; // 毎秒減衰
    private float powerGainPerInput = 0.12f;

    private int lastDirection = -1; // 0=上, 1=右, 2=下, 3=左
    private float lastInputTime = -999f;
    private float inputTimeout = 0.5f; // この時間内に次の入力がないとリセット

    private float barWidth = 760f;

    // PixelFont用
    private MicroGameIMGUIHelper guiHelper;
    private string promptText;

    // 矢印表示用
    private static readonly string[] ArrowSymbols = { "^", ">", "v", "<" };

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        power = 0f;
        lastDirection = -1;
        lastInputTime = -999f;

        // 難易度調整
        powerDecay = 0.3f + 0.1f * ctx.roundIndex * ctx.speedMul;
        powerGainPerInput = Mathf.Max(0.08f, 0.12f / Mathf.Max(0.5f, ctx.speedMul));
        powerRequired = 1f;

        root = new GameObject("Spin", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // IMGUI用ヘルパーを追加
        guiHelper = root.AddComponent<MicroGameIMGUIHelper>();
        guiHelper.Initialize(GetPromptText);
        guiHelper.SetTimer(() => time, () => limit);

        // パワーバー外枠
        var barOuter = AcUi.Image(root.transform, "BarOuter", new Color(0f, 0f, 0f, 1f));
        powerBar = barOuter.rectTransform;
        AcUi.Place(powerBar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(barWidth + 16f, 70f));

        var barBg = AcUi.Image(barOuter.transform, "BarBg", new Color(0.12f, 0.13f, 0.2f, 1f));
        AcUi.Stretch(barBg.rectTransform, 6f);

        // パワーフィル
        var fill = AcUi.Image(barBg.transform, "Fill", ctx.accent);
        powerFill = fill.rectTransform;
        powerFill.anchorMin = new Vector2(0f, 0.5f);
        powerFill.anchorMax = new Vector2(0f, 0.5f);
        powerFill.pivot = new Vector2(0f, 0.5f);
        powerFill.anchoredPosition = new Vector2(2f, 0f);
        powerFill.sizeDelta = new Vector2(0f, 52f);

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (lastDirection == -1)
        {
            promptText = "ROTATE ARROWS!";
        }
        else
        {
            promptText = $"KEEP GOING! {(int)(power * 100)}%";
        }
    }

    public string GetPromptText() => promptText;

    public bool Tick(float dt)
    {
        time += dt;

        // パワー減衰
        power -= powerDecay * dt;
        power = Mathf.Max(0f, power);

        // 矢印キー入力チェック
        var kb = Keyboard.current;
        if (kb != null)
        {
            int currentDirection = -1;

            if (kb.upArrowKey.wasPressedThisFrame) currentDirection = 0;
            else if (kb.rightArrowKey.wasPressedThisFrame) currentDirection = 1;
            else if (kb.downArrowKey.wasPressedThisFrame) currentDirection = 2;
            else if (kb.leftArrowKey.wasPressedThisFrame) currentDirection = 3;

            if (currentDirection != -1)
            {
                float timeSinceLastInput = time - lastInputTime;

                if (lastDirection == -1)
                {
                    // 最初の入力
                    lastDirection = currentDirection;
                    lastInputTime = time;
                }
                else if (timeSinceLastInput <= inputTimeout)
                {
                    // 連続入力チェック（時計回りまたは反時計回り）
                    int expectedClockwise = (lastDirection + 1) % 4;
                    int expectedCounterClockwise = (lastDirection - 1 + 4) % 4;

                    if (currentDirection == expectedClockwise || currentDirection == expectedCounterClockwise)
                    {
                        // 正しい方向！
                        power += powerGainPerInput;
                        power = Mathf.Min(power, powerRequired);
                        lastDirection = currentDirection;
                        lastInputTime = time;
                    }
                    else
                    {
                        // 間違った方向、リセット
                        lastDirection = currentDirection;
                        lastInputTime = time;
                    }
                }
                else
                {
                    // タイムアウト、リセット
                    lastDirection = currentDirection;
                    lastInputTime = time;
                }

                UpdatePrompt();
            }
        }

        // タイムアウトチェック
        if (lastDirection != -1 && (time - lastInputTime) > inputTimeout)
        {
            lastDirection = -1;
            UpdatePrompt();
        }

        // バー更新
        powerFill.sizeDelta = new Vector2((barWidth - 4f) * power, 52f);

        // 成功判定
        if (power >= powerRequired)
        {
            Succeeded = true;
            return true;
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
