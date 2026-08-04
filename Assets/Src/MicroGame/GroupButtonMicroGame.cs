using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 全員参加型のボタン押しミニゲーム(PixelFontRenderer使用)。
/// 4人が同時に1つのゲームを遊び、各自の割り当てキー(1/2/3/4)を制限時間内に押せば自分の攻撃が成立する。
/// 押した瞬間に <see cref="MicroGameContext.OnPlayerAttack"/> が呼ばれる。1人につき1命中。
/// </summary>
public class GroupButtonMicroGame : IMicroGame
{
    public string DisplayName => "数字キーを押せ!";
    public bool Succeeded { get; private set; }

    private GameObject root;
    private bool[] landed;
    private MicroGameContext ctx;
    private float timeLeft;
    private int playerCount;

    // PixelFont用
    private GroupButtonIMGUIHelper guiHelper;
    private string[] slotTexts;
    private Color[] slotColors;

    public void Begin(MicroGameContext c)
    {
        ctx = c;
        playerCount = Mathf.Max(1, c.playerCount);
        timeLeft = c.timeLimit > 0f ? c.timeLimit : 4.5f;
        landed = new bool[playerCount];
        slotTexts = new string[playerCount];
        slotColors = new Color[playerCount];

        for (int i = 0; i < playerCount; i++)
        {
            slotTexts[i] = $"{i + 1}P [{i + 1}]";
            slotColors[i] = ResolveColor(i);
        }

        root = new GameObject("GroupButton");
        root.transform.SetParent(c.uiRoot, false);

        // IMGUI用ヘルパーを追加
        guiHelper = root.AddComponent<GroupButtonIMGUIHelper>();
        guiHelper.Initialize(this, playerCount, slotColors);
    }

    public string GetTitle() => "1 2 3 4";
    public string GetTimer() => $"{Mathf.Max(0f, timeLeft):F1}";
    public string[] GetSlotTexts() => slotTexts;
    public Color[] GetSlotColors() => slotColors;
    public bool[] GetLanded() => landed;

    public bool Tick(float dt)
    {
        timeLeft -= dt;

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
                slotTexts[i] = $"{i + 1}P MISS";
                slotColors[i] = new Color(0.55f, 0.55f, 0.6f, 1f);
            }
            if (guiHelper != null) guiHelper.MarkDirty();

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
        slotTexts[playerIndex] = $"{playerIndex + 1}P HIT!";
        slotColors[playerIndex] = new Color(1f, 0.95f, 0.45f, 1f); // 明るいゴールド
        if (guiHelper != null) guiHelper.MarkDirty();

        ctx?.OnPlayerAttack?.Invoke(playerIndex);
    }

    public void Cleanup()
    {
        if (root != null) Object.Destroy(root);
        root = null;
        ctx = null;
    }

    private Color ResolveColor(int playerIndex)
    {
        if (ctx != null && ctx.playerColors != null && playerIndex < ctx.playerColors.Length)
            return ctx.playerColors[playerIndex];
        switch (playerIndex)
        {
            case 0: return new Color(1f, 0.72f, 0.72f, 1f);   // 薄い赤
            case 1: return new Color(0.72f, 0.83f, 1f, 1f);   // 薄い青
            case 2: return new Color(0.72f, 1f, 0.72f, 1f);   // 薄い緑
            default: return new Color(1f, 0.93f, 0.72f, 1f);  // 薄い黄
        }
    }
}

/// <summary>GroupButtonMicroGame用のIMGUIヘルパー</summary>
public class GroupButtonIMGUIHelper : MonoBehaviour
{
    private GroupButtonMicroGame game;
    private int playerCount;
    private Color[] baseColors;

    private Texture2D titleTexture;
    private Texture2D timerTexture;
    private Texture2D[] slotTextures;
    private string cachedTimer;
    private string[] cachedSlots;
    private int pixelScale;
    private bool isDirty = true;

    public void Initialize(GroupButtonMicroGame g, int count, Color[] colors)
    {
        game = g;
        playerCount = count;
        baseColors = colors;
        pixelScale = Mathf.Max(3, Screen.height / 240);

        slotTextures = new Texture2D[count];
        cachedSlots = new string[count];

        // タイトルテクスチャ作成
        titleTexture = PixelFontRenderer.RenderText("1  2  3  4", Color.white, pixelScale + 3);
    }

    public void MarkDirty()
    {
        isDirty = true;
    }

    void OnDestroy()
    {
        if (titleTexture != null) Destroy(titleTexture);
        if (timerTexture != null) Destroy(timerTexture);
        if (slotTextures != null)
        {
            foreach (var tex in slotTextures)
                if (tex != null) Destroy(tex);
        }
    }

    void OnGUI()
    {
        if (game == null) return;

        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // タイトル
        if (titleTexture != null)
        {
            float x = centerX - titleTexture.width / 2f;
            float y = centerY - 180f;
            GUI.DrawTexture(new Rect(x, y, titleTexture.width, titleTexture.height), titleTexture);
        }

        // タイマー更新
        string timerText = game.GetTimer();
        if (timerText != cachedTimer || timerTexture == null)
        {
            if (timerTexture != null) Destroy(timerTexture);
            timerTexture = PixelFontRenderer.RenderText(timerText, new Color(1f, 0.9f, 0.4f), pixelScale + 2);
            cachedTimer = timerText;
        }
        // 右中央・金枠で描画(共通)
        AcImgui.DrawTimerBox(timerTexture);

        // スロットテキスト更新
        string[] slots = game.GetSlotTexts();
        Color[] colors = game.GetSlotColors();

        for (int i = 0; i < playerCount && i < slots.Length; i++)
        {
            if (isDirty || slots[i] != cachedSlots[i] || slotTextures[i] == null)
            {
                if (slotTextures[i] != null) Destroy(slotTextures[i]);
                slotTextures[i] = PixelFontRenderer.RenderText(slots[i], colors[i], pixelScale + 1);
                cachedSlots[i] = slots[i];
            }
        }
        isDirty = false;

        // スロット描画（横並び）
        float totalWidth = 0f;
        float gap = pixelScale * 12;
        foreach (var tex in slotTextures)
            if (tex != null) totalWidth += tex.width + gap;
        totalWidth -= gap;

        float slotX = centerX - totalWidth / 2f;
        float slotY = centerY + 20f;

        for (int i = 0; i < slotTextures.Length; i++)
        {
            var tex = slotTextures[i];
            if (tex != null)
            {
                GUI.DrawTexture(new Rect(slotX, slotY, tex.width, tex.height), tex);
                slotX += tex.width + gap;
            }
        }
    }
}
