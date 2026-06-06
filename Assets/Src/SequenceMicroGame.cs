using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>順番入力: 表示された矢印の順に入力。interference で早く隠れる。</summary>
public class SequenceMicroGame : IMicroGame
{
    public string DisplayName => "コマンドれんぞく！";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private Text prompt;
    private Text seqLabel;
    private float time, limit;
    private readonly List<int> seq = new List<int>(); // 0=↑1=↓2=←3=→
    private int idx;
    private float hideAt;     // この時刻でシーケンス表示を隠す
    private bool hidden;
    private static readonly string[] Arrow = { "↑", "↓", "←", "→" };

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        idx = 0;
        hidden = false;
        int len = Mathf.Clamp(ctx.buttonCount + ctx.roundIndex, 3, 9);
        seq.Clear();
        for (int i = 0; i < len; i++) seq.Add(ctx.rng.Next(4));
        // 妨害が強い/速いほど早く隠れる
        hideAt = Mathf.Lerp(limit * 0.6f, 0.6f, Mathf.Clamp01(ctx.interference + (ctx.speedMul - 1f) * 0.3f));

        root = new GameObject("Sequence", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        prompt = AcUi.Label(root.transform, "P", 56, Color.white, TextAnchor.MiddleCenter, true, true);
        AcUi.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1300f, 90f));
        prompt.text = "この順に入力！(矢印キー)";

        seqLabel = AcUi.Label(root.transform, "Seq", 80, ctx.accent, TextAnchor.MiddleCenter, true, true);
        AcUi.Place(seqLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 140f));
        Refresh();
    }

    private void Refresh()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < seq.Count; i++)
        {
            if (hidden) sb.Append(i < idx ? Arrow[seq[i]] : "?");
            else sb.Append(Arrow[seq[i]]);
            if (i < seq.Count - 1) sb.Append(' ');
        }
        seqLabel.text = sb.ToString();
    }

    public bool Tick(float dt)
    {
        time += dt;
        if (!hidden && time >= hideAt) { hidden = true; Refresh(); }

        var kb = Keyboard.current;
        if (kb != null)
        {
            int pressed = -1;
            if (kb.upArrowKey.wasPressedThisFrame) pressed = 0;
            else if (kb.downArrowKey.wasPressedThisFrame) pressed = 1;
            else if (kb.leftArrowKey.wasPressedThisFrame) pressed = 2;
            else if (kb.rightArrowKey.wasPressedThisFrame) pressed = 3;

            if (pressed >= 0)
            {
                if (pressed == seq[idx])
                {
                    idx++;
                    Refresh();
                    if (idx >= seq.Count) { Succeeded = true; return true; }
                }
                else { Succeeded = false; return true; }
            }
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
