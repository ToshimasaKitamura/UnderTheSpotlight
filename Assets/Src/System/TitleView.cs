using UnityEngine;

/// <summary>
/// タイトル画面のUI表示を担当（8bitドット絵風）。
/// ピクセルフォントとレトロな演出で描画します。
/// </summary>
public class TitleView : MonoBehaviour
{
    [Header("テキスト設定")]
    [SerializeField] private string titleText = "UNDER THE SPOTLIGHT";
    [SerializeField] private string subtitleText = "4 PLAYERS CO-OP ACTION";
    [SerializeField] private string pressKeyText = "PRESS ANY KEY";

    [Header("メニュー設定")]
    [SerializeField] private string[] menuItems = { "START GAME", "OPTIONS", "EXIT" };

    // 色はテーマ(GameColorScheme)に一元化。正典: Docs/Specifications/デザインテーマ.md
    //   背景=Background グラデ / タイトル・選択・スター=Accent / メニュー=TextOnDark / サブ=TextMuted

    // 参照
    private TitleSceneController controller;

    // テクスチャ
    private Texture2D bgTexture;
    private Texture2D titleTexture;
    private Texture2D subtitleTexture;
    private Texture2D pressKeyTexture;
    private Texture2D[] menuTextures;
    private Texture2D[] menuSelectTextures;
    private Texture2D cursorTexture;
    private Texture2D[] starTextures;
    private Texture2D boxTexture;
    private Texture2D versionTexture;
    private Texture2D controlHintTexture;

    // アニメーション
    private float blinkTimer;
    private float starTimer;
    private Vector2[] starPositions;
    private float[] starPhases;

    // ピクセルスケール（画面解像度に応じて調整）
    private int pixelScale = 3;

    private void Awake()
    {
        controller = GetComponent<TitleSceneController>();
    }

    private void Start()
    {
        CalculatePixelScale();
        CreateTextures();
        InitializeStars();
    }

    private void OnDestroy()
    {
        DestroyTextures();
        PixelFontRenderer.ClearCache();
    }

    private void CalculatePixelScale()
    {
        // 画面サイズに応じてスケール調整（基準: 1080p = scale 3）
        pixelScale = Mathf.Max(2, Screen.height / 360);
    }

    private void CreateTextures()
    {
        // 背景（ディザリンググラデーション）
        bgTexture = PixelFontRenderer.CreateDitheredGradient(
            Screen.width / 4, Screen.height / 4,
            GameColorScheme.BackgroundTop, GameColorScheme.BackgroundBottom, 2);

        // タイトル
        titleTexture = PixelFontRenderer.RenderText(titleText, GameColorScheme.Accent, pixelScale + 2);
        subtitleTexture = PixelFontRenderer.RenderText(subtitleText, GameColorScheme.TextMuted, pixelScale);
        pressKeyTexture = PixelFontRenderer.RenderText(pressKeyText, GameColorScheme.TextOnDark, pixelScale);

        // メニュー項目
        menuTextures = new Texture2D[menuItems.Length];
        menuSelectTextures = new Texture2D[menuItems.Length];
        for (int i = 0; i < menuItems.Length; i++)
        {
            menuTextures[i] = PixelFontRenderer.RenderText(menuItems[i], GameColorScheme.TextOnDark, pixelScale);
            menuSelectTextures[i] = PixelFontRenderer.RenderText("> " + menuItems[i] + " <", GameColorScheme.Accent, pixelScale);
        }

        // カーソル
        cursorTexture = PixelFontRenderer.CreatePixelStar(pixelScale * 8, GameColorScheme.Accent);

        // スター（背景装飾）
        starTextures = new Texture2D[3];
        Color starBase = GameColorScheme.Accent;
        for (int i = 0; i < 3; i++)
        {
            int size = pixelScale * (4 + i * 2);
            float brightness = 0.4f + i * 0.2f;
            starTextures[i] = PixelFontRenderer.CreatePixelStar(size,
                new Color(starBase.r * brightness, starBase.g * brightness, starBase.b * brightness));
        }

        // ボックス（メニュー背景）
        Color boxFill = GameColorScheme.Surface; boxFill.a = 0.9f;
        boxTexture = PixelFontRenderer.CreatePixelBox(400, 200, boxFill, GameColorScheme.Main, pixelScale);

        // バージョン
        versionTexture = PixelFontRenderer.RenderText("V0.1.0", GameColorScheme.TextMuted, pixelScale - 1);

        // 操作説明
        controlHintTexture = PixelFontRenderer.RenderText(
            "ARROW:SELECT  Z:OK  X:BACK", GameColorScheme.TextMuted, pixelScale - 1);
    }

    private void DestroyTextures()
    {
        if (bgTexture != null) Destroy(bgTexture);
        if (titleTexture != null) Destroy(titleTexture);
        if (subtitleTexture != null) Destroy(subtitleTexture);
        if (pressKeyTexture != null) Destroy(pressKeyTexture);
        if (cursorTexture != null) Destroy(cursorTexture);
        if (boxTexture != null) Destroy(boxTexture);
        if (versionTexture != null) Destroy(versionTexture);
        if (controlHintTexture != null) Destroy(controlHintTexture);

        if (menuTextures != null)
        {
            foreach (var tex in menuTextures) if (tex != null) Destroy(tex);
        }
        if (menuSelectTextures != null)
        {
            foreach (var tex in menuSelectTextures) if (tex != null) Destroy(tex);
        }
        if (starTextures != null)
        {
            foreach (var tex in starTextures) if (tex != null) Destroy(tex);
        }
    }

    private void InitializeStars()
    {
        int starCount = 30;
        starPositions = new Vector2[starCount];
        starPhases = new float[starCount];

        System.Random rng = new System.Random(42);
        for (int i = 0; i < starCount; i++)
        {
            starPositions[i] = new Vector2(
                (float)rng.NextDouble(),
                (float)rng.NextDouble()
            );
            starPhases[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
        }
    }

    private void Update()
    {
        blinkTimer += Time.deltaTime;
        starTimer += Time.deltaTime;
    }

    private void OnGUI()
    {
        if (controller == null) return;

        float fadeAlpha = controller.FadeProgress;
        var state = controller.CurrentState;

        Color originalColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fadeAlpha);

        DrawBackground();
        DrawStars();
        DrawTitle();

        switch (state)
        {
            case TitleSceneController.TitleState.WaitForStart:
                DrawPressAnyKey();
                break;
            case TitleSceneController.TitleState.Menu:
                DrawMenu();
                break;
        }

        DrawVersion();

        GUI.color = originalColor;
    }

    private void DrawBackground()
    {
        if (bgTexture == null) return;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTexture, ScaleMode.StretchToFill);
    }

    private void DrawStars()
    {
        if (starTextures == null || starPositions == null) return;

        for (int i = 0; i < starPositions.Length; i++)
        {
            // 点滅アニメーション
            float twinkle = Mathf.Sin(starTimer * 2f + starPhases[i]);
            if (twinkle < 0.3f) continue; // 一部の時間は消える

            float alpha = 0.3f + twinkle * 0.4f;
            Color c = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * alpha);

            int texIndex = i % starTextures.Length;
            Texture2D starTex = starTextures[texIndex];
            if (starTex == null) continue;

            float x = starPositions[i].x * Screen.width;
            float y = starPositions[i].y * Screen.height;

            // 少しゆらゆら動く
            x += Mathf.Sin(starTimer + starPhases[i]) * 2f;
            y += Mathf.Cos(starTimer * 0.7f + starPhases[i]) * 2f;

            GUI.DrawTexture(new Rect(x, y, starTex.width, starTex.height), starTex);

            GUI.color = c;
        }
    }

    private void DrawTitle()
    {
        if (titleTexture == null) return;

        float centerX = Screen.width / 2f;
        float titleY = Screen.height * 0.22f;

        // タイトルを上下にゆっくり揺らす（8bit風に2pxずつ）
        float bobOffset = Mathf.Round(Mathf.Sin(Time.time * 1.5f) * 2f) * pixelScale;

        // タイトル
        Rect titleRect = new Rect(
            centerX - titleTexture.width / 2f,
            titleY + bobOffset,
            titleTexture.width,
            titleTexture.height);
        GUI.DrawTexture(titleRect, titleTexture);

        // サブタイトル
        if (subtitleTexture != null)
        {
            Rect subRect = new Rect(
                centerX - subtitleTexture.width / 2f,
                titleY + titleTexture.height + pixelScale * 4,
                subtitleTexture.width,
                subtitleTexture.height);
            GUI.DrawTexture(subRect, subtitleTexture);
        }
    }

    private void DrawPressAnyKey()
    {
        if (pressKeyTexture == null) return;

        // 点滅（8bit風にON/OFF）
        bool visible = Mathf.Sin(blinkTimer * 3f) > 0;
        if (!visible) return;

        float centerX = Screen.width / 2f;
        float y = Screen.height * 0.65f;

        Rect rect = new Rect(
            centerX - pressKeyTexture.width / 2f,
            y,
            pressKeyTexture.width,
            pressKeyTexture.height);
        GUI.DrawTexture(rect, pressKeyTexture);
    }

    private void DrawMenu()
    {
        if (menuTextures == null || controller == null) return;

        float centerX = Screen.width / 2f;
        float startY = Screen.height * 0.52f;
        float spacing = pixelScale * 14;

        int selection = (int)controller.CurrentSelection;

        for (int i = 0; i < menuTextures.Length; i++)
        {
            bool isSelected = i == selection;
            Texture2D tex = isSelected ? menuSelectTextures[i] : menuTextures[i];
            if (tex == null) continue;

            float y = startY + i * spacing;

            // 選択中はX方向に揺れる
            float xOffset = 0;
            if (isSelected)
            {
                xOffset = Mathf.Round(Mathf.Sin(Time.time * 8f)) * pixelScale;
            }

            Rect rect = new Rect(
                centerX - tex.width / 2f + xOffset,
                y,
                tex.width,
                tex.height);
            GUI.DrawTexture(rect, tex);
        }

        // 操作説明
        DrawControlHints();
    }

    private void DrawControlHints()
    {
        if (controlHintTexture == null) return;

        float y = Screen.height - controlHintTexture.height - pixelScale * 8;
        Rect rect = new Rect(
            (Screen.width - controlHintTexture.width) / 2f,
            y,
            controlHintTexture.width,
            controlHintTexture.height);
        GUI.DrawTexture(rect, controlHintTexture);
    }

    private void DrawVersion()
    {
        if (versionTexture == null) return;

        Rect rect = new Rect(
            Screen.width - versionTexture.width - pixelScale * 4,
            Screen.height - versionTexture.height - pixelScale * 4,
            versionTexture.width,
            versionTexture.height);
        GUI.DrawTexture(rect, versionTexture);
    }
}
