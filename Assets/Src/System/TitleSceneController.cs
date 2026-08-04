using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// タイトルシーンのコントローラー（仮実装・8bitドット絵風）。
/// ゲームオーバー画面(ResultSceneController)と同じ描画スタイルで、
/// タイトルロゴ・スター背景・メニューボタンを表示し、シーン遷移を行います。
/// </summary>
public class TitleSceneController : MonoBehaviour
{
    public enum TitleState
    {
        WaitForStart,
        Menu
    }

    [Header("表示設定")]
    [SerializeField] private string titleText = "UNDER THE SPOTLIGHT";
    [SerializeField] private string subtitleText = "- 4人協力弾幕アクション -";

    // 色はテーマ(GameColorScheme)に一元化。正典: Docs/Specifications/デザインテーマ.md

    public TitleState CurrentState { get; private set; } = TitleState.WaitForStart;
    public float FadeProgress { get; private set; } = 0f;
    public int CurrentSelection { get; private set; } = 0;

    private float inputDelay = 0.8f;
    private float elapsedTime;
    private bool canAcceptInput;

    private float fadeTimer = 0f;
    private float fadeDuration = 0.5f;

    private bool wasStickUp = false;
    private bool wasStickDown = false;

    // テクスチャ
    private Texture2D bgTexture;
    private Texture2D titleTexture;

    // スター(背景演出)
    private float starTimer;
    private Vector2[] starPositions;
    private float[] starPhases;

    private int pixelScale = 3;

    private void Start()
    {
        // バトルシーンを直接再生した時などでも遷移が機能するよう、無ければ生成しておく。
        if (GameSceneManager.Instance == null)
        {
            Debug.LogWarning("[TitleSceneController] GameSceneManager が見つからないため自動生成します。");
        }

        elapsedTime = 0f;
        canAcceptInput = false;
        pixelScale = Mathf.Max(2, Screen.height / 360);

        CreateTextures();
        InitializeStars();
    }

    private void OnDestroy()
    {
        if (bgTexture != null) Destroy(bgTexture);
        if (titleTexture != null) Destroy(titleTexture);
    }

    private void CreateTextures()
    {
        bgTexture = PixelFontRenderer.CreateDitheredGradient(
            Screen.width / 4, Screen.height / 4,
            GameColorScheme.BackgroundTop, GameColorScheme.BackgroundBottom, 2);

        // タイトルロゴは ASCII のためピクセルフォントで描画(GAME OVER と同じ方式)。
        titleTexture = PixelFontRenderer.RenderText(titleText, GameColorScheme.Accent, pixelScale + 2);
    }

    private void InitializeStars()
    {
        int starCount = 30;
        starPositions = new Vector2[starCount];
        starPhases = new float[starCount];

        System.Random rng = new System.Random(2026);
        for (int i = 0; i < starCount; i++)
        {
            starPositions[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
            starPhases[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
        }
    }

    private void Update()
    {
        starTimer += Time.deltaTime;

        if (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            FadeProgress = Mathf.Clamp01(fadeTimer / fadeDuration);
        }

        if (!canAcceptInput)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= inputDelay) canAcceptInput = true;
            return;
        }

        var kb = Keyboard.current;
        var gp = Gamepad.current;

        bool confirmPressed = false;
        bool backPressed = false;
        bool upPressed = false;
        bool downPressed = false;

        bool stickUp = false;
        bool stickDown = false;
        if (gp != null)
        {
            float stickY = gp.leftStick.y.ReadValue();
            stickUp = stickY > 0.5f;
            stickDown = stickY < -0.5f;
        }

        if (kb != null)
        {
            confirmPressed |= kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame;
            backPressed |= kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame;
            upPressed |= kb.upArrowKey.wasPressedThisFrame;
            downPressed |= kb.downArrowKey.wasPressedThisFrame;
        }

        if (gp != null)
        {
            confirmPressed |= gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame;
            backPressed |= gp.buttonEast.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame;
            upPressed |= gp.dpad.up.wasPressedThisFrame || (stickUp && !wasStickUp);
            downPressed |= gp.dpad.down.wasPressedThisFrame || (stickDown && !wasStickDown);

            wasStickUp = stickUp;
            wasStickDown = stickDown;
        }

        bool anyKeyPressed = false;
        if (kb != null && kb.anyKey.wasPressedThisFrame)
        {
            anyKeyPressed = true;
        }
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame ||
                gp.buttonEast.wasPressedThisFrame ||
                gp.buttonWest.wasPressedThisFrame ||
                gp.buttonNorth.wasPressedThisFrame ||
                gp.startButton.wasPressedThisFrame ||
                gp.selectButton.wasPressedThisFrame ||
                gp.dpad.up.wasPressedThisFrame ||
                gp.dpad.down.wasPressedThisFrame ||
                gp.dpad.left.wasPressedThisFrame ||
                gp.dpad.right.wasPressedThisFrame)
            {
                anyKeyPressed = true;
            }
        }

        if (CurrentState == TitleState.WaitForStart)
        {
            if (anyKeyPressed || confirmPressed)
            {
                CurrentState = TitleState.Menu;
                CurrentSelection = 0;
            }
        }
        else if (CurrentState == TitleState.Menu)
        {
            if (upPressed)
            {
                CurrentSelection = (CurrentSelection - 1 + 3) % 3;
            }
            else if (downPressed)
            {
                CurrentSelection = (CurrentSelection + 1) % 3;
            }
            else if (confirmPressed)
            {
                ExecuteMenuAction(CurrentSelection);
            }
            else if (backPressed)
            {
                CurrentState = TitleState.WaitForStart;
            }
        }
    }

    private void ExecuteMenuAction(int selection)
    {
        switch (selection)
        {
            case 0: // START GAME
                StartGame();
                break;
            case 1: // OPTIONS
                Debug.Log("[TitleSceneController] OPTIONS Selected (Not implemented)");
                break;
            case 2: // EXIT
                QuitGame();
                break;
        }
    }

    private void StartGame()
    {
        var gm = GameSceneManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[TitleSceneController] GameSceneManager.Instance が取得できず、ワールドマップへ遷移できません。");
            return;
        }
        gm.GoToWorldMap();
    }

    private void OnGUI()
    {
        if (GetComponent<TitleView>() != null) return;

        DrawBackground();
        DrawStars();
        DrawTitle();
        DrawButtons();
    }

    private void DrawBackground()
    {
        if (bgTexture == null) return;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTexture, ScaleMode.StretchToFill);
    }

    private void DrawStars()
    {
        if (starPositions == null) return;
        for (int i = 0; i < starPositions.Length; i++)
        {
            float twinkle = (Mathf.Sin(starTimer * 2f + starPhases[i]) + 1f) * 0.5f;
            if (twinkle < 0.3f) continue;

            float x = starPositions[i].x * Screen.width;
            float y = starPositions[i].y * Screen.height * 0.85f;
            float size = pixelScale * (1f + twinkle);

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 0.9f, twinkle * 0.7f);
            GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }

    private void DrawTitle()
    {
        if (titleTexture == null) return;

        float centerX = Screen.width / 2f;
        float y = Screen.height * 0.30f;
        float scale = Mathf.Min(1f, (Screen.width * 0.86f) / titleTexture.width);
        float w = titleTexture.width * scale;
        float h = titleTexture.height * scale;

        GUI.DrawTexture(new Rect(centerX - w / 2f, y - h / 2f, w, h), titleTexture);

        // サブタイトル(日本語)は misaki フォントの GUI.Label で描画。
        var style = SubtitleStyle();
        var size = style.CalcSize(new GUIContent(subtitleText));
        GUI.Label(new Rect(centerX - size.x / 2f, y + h / 2f + pixelScale * 4, size.x, size.y), subtitleText, style);
    }

    private GUIStyle cachedSubtitleStyle;
    private GUIStyle SubtitleStyle()
    {
        if (cachedSubtitleStyle == null)
        {
            cachedSubtitleStyle = new GUIStyle(GUI.skin.label);
            var f = GameFont.Body();
            if (f != null) cachedSubtitleStyle.font = f;
            cachedSubtitleStyle.fontSize = Mathf.Max(14, pixelScale * 6);
            cachedSubtitleStyle.normal.textColor = GameColorScheme.TextMuted;
            cachedSubtitleStyle.alignment = TextAnchor.MiddleCenter;
        }
        return cachedSubtitleStyle;
    }

    private GUIStyle cachedBtnStyle;
    private GUIStyle ButtonStyle()
    {
        if (cachedBtnStyle == null)
        {
            cachedBtnStyle = new GUIStyle(GUI.skin.button);
            var f = GameFont.Body();          // 日本語対応フォント(IMGUIネイティブ)
            if (f != null) cachedBtnStyle.font = f;
            cachedBtnStyle.fontSize = Mathf.Max(18, pixelScale * 7);
            cachedBtnStyle.fontStyle = FontStyle.Bold;
        }
        return cachedBtnStyle;
    }

    private static Rect BtnRect(float cx, float y, float w, float h) => new Rect(cx - w / 2f, y, w, h);

    private void DrawButtons()
    {
        if (!canAcceptInput) return;

        var style = ButtonStyle();
        float cx = Screen.width / 2f;
        float bw = Mathf.Min(Screen.width * 0.55f, 400f);
        float bh = Mathf.Max(48f, Screen.height * 0.075f);
        float gap = bh * 0.28f;
        float y = Screen.height * 0.6f;

        if (GUI.Button(BtnRect(cx, y, bw, bh), "ゲームスタート", style)) StartGame();
        if (GUI.Button(BtnRect(cx, y + (bh + gap), bw, bh), "終了", style)) QuitGame();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
