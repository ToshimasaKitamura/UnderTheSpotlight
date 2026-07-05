using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// リザルトシーンのコントローラー（8bitドット絵風）。
/// 勝敗結果をピクセルフォントで表示し、次の行動を選択させます。
/// </summary>
public class ResultSceneController : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private string victoryText = "VICTORY!";
    [SerializeField] private string defeatText = "GAME OVER";

    [Header("8bit カラー設定")]
    [SerializeField] private Color victoryColor = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private Color defeatColor = new Color(0.8f, 0.3f, 0.3f);
    [SerializeField] private Color bgColor1 = new Color(0.02f, 0.02f, 0.08f);
    [SerializeField] private Color bgColor2 = new Color(0.0f, 0.0f, 0.03f);

    private bool isVictory;
    private float inputDelay = 1.5f;
    private float elapsedTime;
    private bool canAcceptInput;

    // テクスチャ
    private Texture2D bgTexture;
    private Texture2D resultTexture;
    private Texture2D continueTexture;
    private Texture2D titleTexture;
    private Texture2D[] starTextures;

    // アニメーション
    private float blinkTimer;
    private float starTimer;
    private Vector2[] starPositions;
    private float[] starPhases;

    private int pixelScale = 3;

    private void Start()
    {
        // GameSceneManager から勝敗を取得
        if (GameSceneManager.Instance != null)
        {
            isVictory = GameSceneManager.Instance.isVictory;
        }

        elapsedTime = 0f;
        canAcceptInput = false;

        pixelScale = Mathf.Max(2, Screen.height / 360);
        CreateTextures();
        InitializeStars();
    }

    private void OnDestroy()
    {
        DestroyTextures();
    }

    private void CreateTextures()
    {
        // 背景
        bgTexture = PixelFontRenderer.CreateDitheredGradient(
            Screen.width / 4, Screen.height / 4, bgColor1, bgColor2, 2);

        // 結果テキスト
        Color resultColor = isVictory ? victoryColor : defeatColor;
        string text = isVictory ? victoryText : defeatText;
        resultTexture = PixelFontRenderer.RenderText(text, resultColor, pixelScale + 3);

        // 操作ガイド
        continueTexture = PixelFontRenderer.RenderText("Z: CONTINUE", Color.white, pixelScale);
        titleTexture = PixelFontRenderer.RenderText("X: TITLE", new Color(0.7f, 0.7f, 0.8f), pixelScale);

        // スター（勝利時のみ多め）
        int starCount = isVictory ? 5 : 2;
        starTextures = new Texture2D[starCount];
        for (int i = 0; i < starCount; i++)
        {
            int size = pixelScale * (6 + i * 2);
            Color starColor = isVictory ? victoryColor : new Color(0.5f, 0.5f, 0.6f);
            starTextures[i] = PixelFontRenderer.CreatePixelStar(size, starColor);
        }
    }

    private void DestroyTextures()
    {
        if (bgTexture != null) Destroy(bgTexture);
        if (resultTexture != null) Destroy(resultTexture);
        if (continueTexture != null) Destroy(continueTexture);
        if (titleTexture != null) Destroy(titleTexture);

        if (starTextures != null)
        {
            foreach (var tex in starTextures)
                if (tex != null) Destroy(tex);
        }
    }

    private void InitializeStars()
    {
        int starCount = isVictory ? 30 : 15;
        starPositions = new Vector2[starCount];
        starPhases = new float[starCount];

        System.Random rng = new System.Random(77);
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

        if (!canAcceptInput)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= inputDelay)
            {
                canAcceptInput = true;
            }
            return;
        }

        // キーボード
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
            {
                GameSceneManager.Instance?.GoToWorldMap();
            }
            if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)
            {
                GameSceneManager.Instance?.GoToTitle();
            }
        }

        // ゲームパッド
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame)
            {
                GameSceneManager.Instance?.GoToWorldMap();
            }
            if (gp.buttonEast.wasPressedThisFrame)
            {
                GameSceneManager.Instance?.GoToTitle();
            }
        }
    }

    private void OnGUI()
    {
        DrawBackground();
        DrawStars();
        DrawResult();
        DrawGuide();
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
            float twinkle = Mathf.Sin(starTimer * (isVictory ? 3f : 1.5f) + starPhases[i]);
            if (twinkle < 0.2f) continue;

            float alpha = 0.2f + twinkle * 0.5f;
            Color c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            int texIndex = i % starTextures.Length;
            Texture2D starTex = starTextures[texIndex];
            if (starTex == null) continue;

            float x = starPositions[i].x * Screen.width;
            float y = starPositions[i].y * Screen.height;

            // 勝利時は上に浮かぶアニメーション
            if (isVictory)
            {
                y -= (starTimer + starPhases[i]) * 20f % Screen.height;
                if (y < -starTex.height) y += Screen.height + starTex.height * 2;
            }

            GUI.DrawTexture(new Rect(x, y, starTex.width, starTex.height), starTex);
            GUI.color = c;
        }
    }

    private void DrawResult()
    {
        if (resultTexture == null) return;

        float centerX = Screen.width / 2f;
        float y = Screen.height * 0.35f;

        // 勝利時は脈動、敗北時は点滅
        float scale = 1f;
        if (isVictory)
        {
            scale = 1f + Mathf.Sin(blinkTimer * 4f) * 0.05f;
        }

        // 敗北時は画面を少し揺らす（最初の1秒だけ）
        float shakeX = 0;
        float shakeY = 0;
        if (!isVictory && elapsedTime < 1f)
        {
            float intensity = 1f - elapsedTime;
            shakeX = Mathf.Round(Mathf.Sin(blinkTimer * 30f) * 3f * intensity) * pixelScale;
            shakeY = Mathf.Round(Mathf.Cos(blinkTimer * 25f) * 2f * intensity) * pixelScale;
        }

        float w = resultTexture.width * scale;
        float h = resultTexture.height * scale;

        GUI.DrawTexture(new Rect(
            centerX - w / 2f + shakeX,
            y - h / 2f + shakeY,
            w, h), resultTexture);

        // サブテキスト（勝利時は追加メッセージ）
        if (isVictory && elapsedTime > 0.5f)
        {
            Texture2D subTex = PixelFontRenderer.RenderText("STAGE CLEAR!", new Color(0.8f, 0.9f, 1f), pixelScale);
            if (subTex != null)
            {
                GUI.DrawTexture(new Rect(
                    centerX - subTex.width / 2f,
                    y + h / 2f + pixelScale * 4,
                    subTex.width, subTex.height), subTex);
            }
        }
    }

    private void DrawGuide()
    {
        if (!canAcceptInput) return;

        // 点滅表示
        bool visible = Mathf.Sin(blinkTimer * 3f) > 0;
        if (!visible) return;

        float centerX = Screen.width / 2f;
        float y = Screen.height * 0.7f;

        // CONTINUE
        if (continueTexture != null)
        {
            GUI.DrawTexture(new Rect(
                centerX - continueTexture.width / 2f,
                y,
                continueTexture.width,
                continueTexture.height), continueTexture);
        }

        // TITLE
        if (titleTexture != null)
        {
            GUI.DrawTexture(new Rect(
                centerX - titleTexture.width / 2f,
                y + continueTexture.height + pixelScale * 4,
                titleTexture.width,
                titleTexture.height), titleTexture);
        }
    }
}
