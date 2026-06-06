using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// 全員参加型のボタン押しミニゲーム(文字だけバージョン)。
/// 4人が同時に1つのゲームを遊び、各自の割り当てキー(1/2/3/4)を制限時間内に押せば自分の攻撃が成立する。
/// 押した瞬間に <see cref="MicroGameContext.OnPlayerAttack"/> が呼ばれる。1人につき1命中。
/// 枠/背景は無し、テキストだけで構成。
/// </summary>
public class GroupButtonMicroGame : IMicroGame
{
    public string DisplayName => "数字キーを押せ!";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private Text titleLabel;
    private Text timerLabel;
    private Text[] slotLabels;
    private bool[] landed;
    private MicroGameContext ctx;
    private float timeLeft;
    private int playerCount;

    public void Begin(MicroGameContext c)
    {
        ctx = c;
        playerCount = Mathf.Max(1, c.playerCount);
        timeLeft = c.timeLimit > 0f ? c.timeLimit : 4.5f;
        landed = new bool[playerCount];
        slotLabels = new Text[playerCount];

        root = new GameObject("GroupButton", typeof(RectTransform));
        root.transform.SetParent(c.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // タイトル(中央上)
        titleLabel = AcUi.Label(root.transform, "Title", 78, Color.white,
            TextAnchor.MiddleCenter, true, true);
        AcUi.Place(titleLabel.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 230f), new Vector2(1600f, 130f));
        titleLabel.text = "自分の番号キーを押せ!";

        // タイマー(中央)
        timerLabel = AcUi.Label(root.transform, "Timer", 50, new Color(1f, 0.9f, 0.4f, 1f),
            TextAnchor.MiddleCenter, true, true);
        AcUi.Place(timerLabel.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 130f), new Vector2(900f, 70f));

        // プレイヤースロット(横並び、テキストだけ)
        float slotW = 320f;
        float gap = 30f;
        float total = playerCount * slotW + (playerCount - 1) * gap;
        float startX = -total * 0.5f + slotW * 0.5f;

        for (int i = 0; i < playerCount; i++)
        {
            Color baseCol = ResolveColor(i);
            float x = startX + i * (slotW + gap);
            var label = AcUi.Label(root.transform, $"Slot_{i}", 44,
                new Color(baseCol.r, baseCol.g, baseCol.b, 0.85f),
                TextAnchor.MiddleCenter, true, true);
            AcUi.Place(label.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(x, -60f), new Vector2(slotW, 220f));
            label.text = $"{i + 1}P\n[ {i + 1} ]\n待機";
            slotLabels[i] = label;
        }
    }

    public bool Tick(float dt)
    {
        timeLeft -= dt;
        if (timerLabel != null) timerLabel.text = $"{Mathf.Max(0f, timeLeft):F1}";

        var kb = Keyboard.current;
        if (kb != null)
        {
            CheckPress(kb.digit1Key, 0);
            CheckPress(kb.digit2Key, 1);
            CheckPress(kb.digit3Key, 2);
            CheckPress(kb.digit4Key, 3);
        }

        // 全員命中で早期終了
        bool allLanded = true;
        for (int i = 0; i < playerCount; i++) if (!landed[i]) { allLanded = false; break; }
        if (allLanded)
        {
            Succeeded = true;
            return true;
        }

        if (timeLeft <= 0f)
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (landed[i]) continue;
                if (slotLabels[i] != null)
                {
                    slotLabels[i].text = $"{i + 1}P\n[ {i + 1} ]\n失敗";
                    slotLabels[i].color = new Color(0.55f, 0.55f, 0.6f, 1f);
                }
            }
            for (int i = 0; i < playerCount; i++) if (landed[i]) { Succeeded = true; return true; }
            Succeeded = false;
            return true;
        }
        return false;
    }

    private void CheckPress(KeyControl key, int playerIndex)
    {
        if (key == null) return;
        if (playerIndex < 0 || playerIndex >= playerCount) return;
        if (landed[playerIndex]) return;
        if (!key.wasPressedThisFrame) return;

        landed[playerIndex] = true;
        if (slotLabels[playerIndex] != null)
        {
            slotLabels[playerIndex].text = $"{playerIndex + 1}P\n[ {playerIndex + 1} ]\n★ 命中!";
            slotLabels[playerIndex].color = new Color(1f, 0.95f, 0.45f, 1f); // 明るいゴールド
        }

        ctx?.OnPlayerAttack?.Invoke(playerIndex);
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
        slotLabels = null;
        ctx = null;
    }

    private Color ResolveColor(int playerIndex)
    {
        if (ctx != null && ctx.playerColors != null && playerIndex < ctx.playerColors.Length)
            return ctx.playerColors[playerIndex];
        switch (playerIndex)
        {
            case 0: return new Color(1f, 0.35f, 0.35f, 1f);
            case 1: return new Color(0.35f, 0.6f, 1f, 1f);
            case 2: return new Color(0.35f, 1f, 0.4f, 1f);
            default: return new Color(1f, 0.85f, 0.35f, 1f);
        }
    }
}
