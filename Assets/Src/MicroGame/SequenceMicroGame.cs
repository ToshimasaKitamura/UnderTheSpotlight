using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>順番入力: 表示された矢印の順に入力。interference で早く隠れる。</summary>
public class SequenceMicroGame : IMicroGame
{
    public string DisplayName => "コマンドれんぞく！";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private float time, limit;
    private readonly List<int> seq = new List<int>(); // 0=↑1=↓2=←3=→
    private int idx;
    private float hideAt;     // この時刻でシーケンス表示を隠す
    private bool hidden;
    private static readonly string[] Arrow = { "^", "v", "<", ">" };

    // PixelFont用
    private SequenceIMGUIHelper guiHelper;
    private string seqText;
    private Color accentColor;

    public void Begin(MicroGameContext ctx)
    {
        limit = ctx.timeLimit;
        time = 0f;
        idx = 0;
        hidden = false;
        accentColor = ctx.accent;
        int len = Mathf.Clamp(ctx.buttonCount + ctx.roundIndex, 3, 9);
        seq.Clear();
        for (int i = 0; i < len; i++) seq.Add(ctx.rng.Next(4));
        // 妨害が強い/速いほど早く隠れる
        hideAt = Mathf.Lerp(limit * 0.6f, 0.6f, Mathf.Clamp01(ctx.interference + (ctx.speedMul - 1f) * 0.3f));

        root = new GameObject("Sequence", typeof(RectTransform));
        root.transform.SetParent(ctx.uiRoot, false);
        AcUi.Stretch(root.GetComponent<RectTransform>());

        // IMGUI用ヘルパーを追加
        guiHelper = root.AddComponent<SequenceIMGUIHelper>();
        guiHelper.Initialize(this, accentColor);

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
        seqText = sb.ToString();
    }

    public string GetPromptText() => "ARROWS!";
    public string GetSeqText() => seqText;
    public float GetTime() => time;
    public float GetLimit() => limit;

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

/// <summary>SequenceMicroGame用のIMGUIヘルパー</summary>
public class SequenceIMGUIHelper : MonoBehaviour
{
    private SequenceMicroGame game;
    private Color accentColor;

    private Texture2D promptTexture;
    private Texture2D seqTexture;
    private Texture2D timerTexture;
    private string cachedSeq;
    private string cachedTimer;
    private int pixelScale;

    public void Initialize(SequenceMicroGame g, Color accent)
    {
        game = g;
        accentColor = accent;
        pixelScale = Mathf.Max(3, Screen.height / 240);

        // プロンプトは固定
        promptTexture = PixelFontRenderer.RenderText(game.GetPromptText(), Color.white, pixelScale + 2);
    }

    void OnDestroy()
    {
        if (promptTexture != null) Destroy(promptTexture);
        if (seqTexture != null) Destroy(seqTexture);
        if (timerTexture != null) Destroy(timerTexture);
    }

    void OnGUI()
    {
        if (game == null) return;

        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // タイマー表示
        float currentTime = game.GetTime();
        float timeLimit = game.GetLimit();
        float remaining = Mathf.Max(0f, timeLimit - currentTime);
        string timerText = $"TIME: {remaining:F1}";

        if (timerText != cachedTimer || timerTexture == null)
        {
            if (timerTexture != null) Destroy(timerTexture);

            Color timerColor = Color.white;
            if (remaining <= 1.0f)
                timerColor = Color.red;
            else if (remaining <= 2.0f)
                timerColor = new Color(1f, 0.6f, 0f);

            timerTexture = PixelFontRenderer.RenderText(timerText, timerColor, pixelScale + 1);
            cachedTimer = timerText;
        }

        if (timerTexture != null)
        {
            float x = Screen.width - timerTexture.width - 20f;
            float y = 20f;
            GUI.DrawTexture(new Rect(x, y, timerTexture.width, timerTexture.height), timerTexture);
        }

        // プロンプト
        if (promptTexture != null)
        {
            float x = centerX - promptTexture.width / 2f;
            float y = centerY - 100f;
            GUI.DrawTexture(new Rect(x, y, promptTexture.width, promptTexture.height), promptTexture);
        }

        // シーケンス更新
        string seq = game.GetSeqText();
        if (seq != cachedSeq || seqTexture == null)
        {
            if (seqTexture != null) Destroy(seqTexture);
            seqTexture = PixelFontRenderer.RenderText(seq, accentColor, pixelScale + 3);
            cachedSeq = seq;
        }

        // シーケンス描画
        if (seqTexture != null)
        {
            float x = centerX - seqTexture.width / 2f;
            float y = centerY;
            GUI.DrawTexture(new Rect(x, y, seqTexture.width, seqTexture.height), seqTexture);
        }
    }
}
