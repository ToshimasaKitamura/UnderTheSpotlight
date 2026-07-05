using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ガード: ペーパーマリオ風の防御アクション。
/// 敵の攻撃が当たる直前にSPACEを押してガード。
/// タイミングが完璧ならダメージ無効化。
/// </summary>
public class GuardMicroGame : IMicroGame
{
    public string DisplayName => "ガード!";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private RectTransform enemy;
    private RectTransform player;
    private RectTransform attackIndicator;

    private float time, limit;
    private float attackStartTime = 0.5f;
    private float attackDuration = 1.5f;
    private float guardWindow = 0.15f;

    private bool attackStarted = false;
    private bool guardPressed = false;
    private float attackProgress = 0f;

    private float enemyStartX = 400f;
    private float enemyEndX = -100f;
    private float playerX = -200f;

    // PixelFont用
    private MicroGameIMGUIHelper guiHelper;
    private string promptText;

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        attackProgress = 0f;
        attackStarted = false;
        guardPressed = false;

        // 難易度調整
        attackDuration = Mathf.Max(0.8f, 1.5f - 0.1f * ctx.roundIndex) / ctx.speedMul;
        guardWindow = Mathf.Clamp(0.15f * ctx.rangeScale - 0.02f * ctx.roundIndex, 0.08f, 0.25f);
        attackStartTime = 0.5f;

        root = new GameObject("Guard", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // IMGUI用ヘルパーを追加
        guiHelper = root.AddComponent<MicroGameIMGUIHelper>();
        guiHelper.Initialize(GetPromptText);
        guiHelper.SetTimer(() => time, () => limit);

        // プレイヤーキャラ
        var playerImg = AcUi.Image(root.transform, "Player", ctx.accent);
        player = playerImg.rectTransform;
        AcUi.Place(player, new Vector2(0.5f, 0.5f), new Vector2(playerX, 0f), new Vector2(70f, 70f));

        // 敵キャラ（突進してくる）
        var enemyImg = AcUi.Image(root.transform, "Enemy", new Color(0.8f, 0.3f, 0.3f, 1f));
        enemy = enemyImg.rectTransform;
        AcUi.Place(enemy, new Vector2(0.5f, 0.5f), new Vector2(enemyStartX, 0f), new Vector2(60f, 60f));

        // 攻撃インジケーター（！マーク的な）
        var indicator = AcUi.Image(root.transform, "Indicator", new Color(1f, 1f, 0.3f, 0.8f));
        attackIndicator = indicator.rectTransform;
        AcUi.Place(attackIndicator, new Vector2(0.5f, 0.5f), new Vector2(enemyStartX, 100f), new Vector2(40f, 80f));
        attackIndicator.gameObject.SetActive(false);

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (!attackStarted)
        {
            promptText = "READY...";
        }
        else if (guardPressed)
        {
            float timeUntilHit = attackDuration - attackProgress;
            if (timeUntilHit <= guardWindow)
            {
                promptText = "PERFECT GUARD!";
            }
            else
            {
                promptText = "TOO EARLY!";
            }
        }
        else
        {
            promptText = "SPACE!";
        }
    }

    public string GetPromptText() => promptText;

    public bool Tick(float dt)
    {
        time += dt;

        // 攻撃開始待ち
        if (!attackStarted)
        {
            if (time >= attackStartTime)
            {
                attackStarted = true;
                attackIndicator.gameObject.SetActive(true);
                UpdatePrompt();
            }
        }
        else
        {
            attackProgress += dt;
            float t = attackProgress / attackDuration;

            // 敵の移動（イーズイン）
            float currentX = Mathf.Lerp(enemyStartX, enemyEndX, EaseIn(t));
            enemy.anchoredPosition = new Vector2(currentX, 0f);
            attackIndicator.anchoredPosition = new Vector2(currentX, 100f);

            // ガード入力チェック
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && !guardPressed)
            {
                guardPressed = true;
                float timeUntilHit = attackDuration - attackProgress;

                // ガードタイミング判定
                if (timeUntilHit <= guardWindow)
                {
                    // パーフェクトガード！
                    Succeeded = true;
                }
                else
                {
                    // 早すぎる
                    Succeeded = false;
                }

                UpdatePrompt();
                return true;
            }

            // 攻撃が当たった
            if (t >= 1f)
            {
                if (!guardPressed)
                {
                    Succeeded = false; // ガードしなかった
                }
                UpdatePrompt();
                return true;
            }

            // インジケーターの点滅（攻撃直前）
            float timeUntilHit2 = attackDuration - attackProgress;
            if (timeUntilHit2 <= guardWindow)
            {
                attackIndicator.gameObject.SetActive((Time.unscaledTime * 12f) % 1f < 0.5f);
            }
        }

        // タイムアップ
        if (time >= limit)
        {
            Succeeded = false;
            return true;
        }

        return false;
    }

    private float EaseIn(float t)
    {
        return t * t * t;
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
