using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ジャンプ: ペーパーマリオ風の連続タイミング押し。
/// 敵に着地する直前にSPACEを押すと追加ダメージ。
/// 最大3回までジャンプ可能。
/// </summary>
public class JumpMicroGame : IMicroGame
{
    public string DisplayName => "ジャンプ!";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private RectTransform player;
    private RectTransform enemy;
    private RectTransform timingIndicator;

    private float time, limit;
    private float jumpHeight = 200f;
    private float enemyY = -80f;
    private float playerStartY = 100f;

    private int maxJumps = 3;
    private int currentJump = 0;
    private int successfulJumps = 0;

    private enum JumpState { Rising, Falling, Landing, NextJump }
    private JumpState state = JumpState.Rising;

    private float jumpTime;
    private float jumpDuration = 0.5f;
    private float landingWindow = 0.15f; // タイミング判定の範囲
    private bool inputThisJump;

    // PixelFont用
    private MicroGameIMGUIHelper guiHelper;
    private string promptText;

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        jumpTime = 0f;
        currentJump = 0;
        successfulJumps = 0;
        state = JumpState.Rising;
        inputThisJump = false;

        // 難易度に応じて調整
        jumpDuration = Mathf.Max(0.3f, 0.5f - 0.05f * ctx.roundIndex);
        landingWindow = Mathf.Clamp(0.15f * ctx.rangeScale - 0.02f * ctx.roundIndex, 0.08f, 0.2f);
        maxJumps = Mathf.Min(3, 2 + ctx.buttonCount / 2);

        root = new GameObject("Jump", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // IMGUI用ヘルパーを追加
        guiHelper = root.AddComponent<MicroGameIMGUIHelper>();
        guiHelper.Initialize(GetPromptText);
        guiHelper.SetTimer(() => time, () => limit);

        // 敵キャラ
        var enemyImg = AcUi.Image(root.transform, "Enemy", new Color(0.8f, 0.3f, 0.3f, 1f));
        enemy = enemyImg.rectTransform;
        AcUi.Place(enemy, new Vector2(0.5f, 0.5f), new Vector2(150f, enemyY), new Vector2(80f, 60f));

        // プレイヤーキャラ（ジャンプする）
        var playerImg = AcUi.Image(root.transform, "Player", ctx.accent);
        player = playerImg.rectTransform;
        AcUi.Place(player, new Vector2(0.5f, 0.5f), new Vector2(-150f, playerStartY), new Vector2(60f, 60f));

        // タイミングインジケーター
        var indicatorImg = AcUi.Image(root.transform, "Timing", new Color(1f, 1f, 0.3f, 0.6f));
        timingIndicator = indicatorImg.rectTransform;
        AcUi.Place(timingIndicator, new Vector2(0.5f, 0.5f), new Vector2(150f, enemyY + 80f), new Vector2(100f, 20f));

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        promptText = $"SPACE!  {successfulJumps}/{maxJumps}";
    }

    public string GetPromptText() => promptText;

    public bool Tick(float dt)
    {
        time += dt;
        jumpTime += dt;

        switch (state)
        {
            case JumpState.Rising:
                UpdateRising();
                break;
            case JumpState.Falling:
                UpdateFalling(dt);
                break;
            case JumpState.Landing:
                UpdateLanding(dt);
                break;
            case JumpState.NextJump:
                UpdateNextJump(dt);
                break;
        }

        // タイムアップ
        if (time >= limit)
        {
            Succeeded = successfulJumps > 0;
            return true;
        }

        return false;
    }

    private void UpdateRising()
    {
        // 上昇フェーズ
        float t = jumpTime / (jumpDuration * 0.5f);
        if (t >= 1f)
        {
            state = JumpState.Falling;
            jumpTime = 0f;
            inputThisJump = false;
        }
        else
        {
            float y = Mathf.Lerp(playerStartY, playerStartY + jumpHeight, EaseOut(t));
            player.anchoredPosition = new Vector2(player.anchoredPosition.x, y);
        }
    }

    private void UpdateFalling(float dt)
    {
        // 落下フェーズ
        float t = jumpTime / (jumpDuration * 0.5f);
        float y = Mathf.Lerp(playerStartY + jumpHeight, enemyY, EaseIn(t));
        player.anchoredPosition = new Vector2(player.anchoredPosition.x, y);

        // 着地タイミング判定
        float timeUntilLanding = (jumpDuration * 0.5f) - jumpTime;
        bool inTimingWindow = timeUntilLanding <= landingWindow;

        // インジケーターの点滅
        if (inTimingWindow)
        {
            timingIndicator.gameObject.SetActive((Time.unscaledTime * 10f) % 1f < 0.5f);
        }
        else
        {
            timingIndicator.gameObject.SetActive(false);
        }

        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame && !inputThisJump)
        {
            inputThisJump = true;
            if (inTimingWindow)
            {
                // 成功！
                successfulJumps++;
                UpdatePrompt();
            }
        }

        if (t >= 1f)
        {
            state = JumpState.Landing;
            jumpTime = 0f;
            timingIndicator.gameObject.SetActive(false);
        }
    }

    private void UpdateLanding(float dt)
    {
        // 着地の小さなバウンス演出
        jumpTime += dt;
        if (jumpTime >= 0.1f)
        {
            currentJump++;
            if (currentJump >= maxJumps)
            {
                // 終了
                Succeeded = successfulJumps >= maxJumps - 1; // 最低2/3成功で成功扱い
                return;
            }
            else
            {
                state = JumpState.NextJump;
                jumpTime = 0f;
            }
        }
    }

    private void UpdateNextJump(float dt)
    {
        // 次のジャンプに移行
        jumpTime += dt;
        if (jumpTime >= 0.2f)
        {
            state = JumpState.Rising;
            jumpTime = 0f;
        }
    }

    private float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private float EaseIn(float t)
    {
        return t * t;
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
