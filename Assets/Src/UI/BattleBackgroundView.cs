using UnityEngine;

/// <summary>
/// バトルシーンの8bitドット風背景を描画。
/// 味方ゾーン(下部/青系)と敵ゾーン(上部/赤紫系)を明確に分ける。
/// カメラ背景を直接変更してキャラクター描画を邪魔しない。
/// </summary>
public class BattleBackgroundView : MonoBehaviour
{
    [Header("ゾーン設定")]
    [Tooltip("味方ゾーンの高さ比率(0-1)。0.4 = 画面下40%")]
    public float allyZoneRatio = 0.4f;
    [Tooltip("敵ゾーンの高さ比率(0-1)。0.35 = 画面上35%")]
    public float enemyZoneRatio = 0.35f;

    [Header("ドットパターン設定")]
    [Tooltip("背景ドットのスケール")]
    public int dotScale = 4;
    [Tooltip("格子パターンのサイズ")]
    public int gridSize = 16;

    [Header("表示設定")]
    [Tooltip("trueの場合はOnGUIで描画（キャラクターの上に被る可能性あり）、falseの場合はSpriteRendererで描画")]
    public bool useOnGUI = false;
    [Tooltip("trueの場合は背景スプライトを生成せず、カメラ背景色のみを使用")]
    public bool cameraBackgroundOnly = true;

    private Texture2D bgTexture;
    private Texture2D allyZoneTexture;
    private Texture2D enemyZoneTexture;
    private Texture2D dividerTexture;

    // 背景用スプライト
    private GameObject bgSpriteObj;
    private SpriteRenderer bgSpriteRenderer;

    // アニメーション
    private float scrollOffset;
    private float pulseTimer;

    void Start()
    {
        // カメラ背景色を設定（必ず実行）
        SetCameraBackgroundColor();

        // カメラ背景色のみモードの場合はここで終了
        if (cameraBackgroundOnly) return;

        CreateTextures();
        if (!useOnGUI)
        {
            SetupSpriteBackground();
        }
    }

    void OnDestroy()
    {
        DestroyTextures();
        if (bgSpriteObj != null)
        {
            Destroy(bgSpriteObj);
        }
    }

    /// <summary>
    /// カメラの背景色を暗い紫に設定
    /// </summary>
    private void SetCameraBackgroundColor()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = GameColorScheme.PrimaryDark;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    /// <summary>
    /// SpriteRendererを使用して背景を描画（キャラクターの後ろに配置）
    /// </summary>
    private void SetupSpriteBackground()
    {
        // カメラ背景色を設定
        SetCameraBackgroundColor();

        // 背景用のスプライトオブジェクトを作成
        bgSpriteObj = new GameObject("BattleBackgroundSprite");
        bgSpriteObj.transform.SetParent(transform);

        // 合成テクスチャを作成
        Texture2D compositeTex = CreateCompositeTexture();
        Sprite sprite = Sprite.Create(
            compositeTex,
            new Rect(0, 0, compositeTex.width, compositeTex.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        bgSpriteRenderer = bgSpriteObj.AddComponent<SpriteRenderer>();
        bgSpriteRenderer.sprite = sprite;
        bgSpriteRenderer.sortingOrder = -1000; // 最背面

        // カメラのサイズに合わせてスケール調整
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;
            float spriteWidth = sprite.bounds.size.x;
            float spriteHeight = sprite.bounds.size.y;
            float scaleX = camWidth / spriteWidth;
            float scaleY = camHeight / spriteHeight;
            bgSpriteObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            bgSpriteObj.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 10f);
        }
    }

    /// <summary>
    /// すべてのゾーンを合成した単一のテクスチャを作成
    /// </summary>
    private Texture2D CreateCompositeTexture()
    {
        // Screen.width/heightがエディタで小さい値を返す場合に備えて最小値を設定
        int screenW = Mathf.Max(Screen.width, 800);
        int screenH = Mathf.Max(Screen.height, 600);
        int w = screenW / dotScale;
        int h = screenH / dotScale;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[w * h];

        // メイン背景を描画
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool dither = ((x + y) % 2 == 0);
                float t = (float)y / h;
                Color baseColor = Color.Lerp(GameColorScheme.PrimaryDark, GameColorScheme.PrimaryMid, t * 0.3f);
                pixels[y * w + x] = dither ? baseColor : Color.Lerp(baseColor, GameColorScheme.PrimaryDark, 0.3f);
            }
        }

        // 敵ゾーンを描画（上部）
        int enemyHeight = Mathf.Max(1, (int)(h * enemyZoneRatio));
        int gridStep = gridSize / dotScale;
        if (gridStep < 2) gridStep = 2;

        for (int y = h - enemyHeight; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool onGrid = (x % gridStep == 0) || ((y - (h - enemyHeight)) % gridStep == 0);
                float t = 1f - (float)(y - (h - enemyHeight)) / enemyHeight;
                Color base1 = Color.Lerp(GameColorScheme.EnemyZoneDark, GameColorScheme.EnemyZoneMid, t * 0.2f);

                if (onGrid)
                {
                    pixels[y * w + x] = Color.Lerp(base1, GameColorScheme.EnemyZoneMid, 0.4f);
                }
                else
                {
                    bool dither = ((x + y) % 2 == 0);
                    pixels[y * w + x] = dither ? base1 : Color.Lerp(base1, GameColorScheme.EnemyZoneDark, 0.15f);
                }
            }
        }

        // 味方ゾーンを描画（下部）
        int allyHeight = Mathf.Max(1, (int)(h * allyZoneRatio));
        for (int y = 0; y < allyHeight; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool onGrid = (x % gridStep == 0) || (y % gridStep == 0);
                float t = (float)y / allyHeight;
                Color base1 = Color.Lerp(GameColorScheme.AllyZoneDark, GameColorScheme.AllyZoneMid, t * 0.2f);

                if (onGrid)
                {
                    pixels[y * w + x] = Color.Lerp(base1, GameColorScheme.AllyZoneMid, 0.4f);
                }
                else
                {
                    bool dither = ((x + y) % 2 == 0);
                    pixels[y * w + x] = dither ? base1 : Color.Lerp(base1, GameColorScheme.AllyZoneDark, 0.15f);
                }
            }
        }

        // 境界線を描画
        Color accentBright = GameColorScheme.AccentBright;
        Color accentDark = GameColorScheme.AccentDark;
        Color frameOuter = GameColorScheme.FrameOuter;

        // 敵ゾーン境界
        int enemyDividerY = h - enemyHeight;
        for (int dy = 0; dy < 4 && enemyDividerY + dy < h; dy++)
        {
            for (int x = 0; x < w; x++)
            {
                if (dy == 1 || dy == 2)
                {
                    bool dot = (x % 4 < 2);
                    pixels[(enemyDividerY + dy) * w + x] = dot ? accentBright : accentDark;
                }
                else
                {
                    pixels[(enemyDividerY + dy) * w + x] = frameOuter;
                }
            }
        }

        // 味方ゾーン境界
        int allyDividerY = allyHeight - 4;
        if (allyDividerY < 0) allyDividerY = 0;
        for (int dy = 0; dy < 4 && allyDividerY + dy < allyHeight; dy++)
        {
            for (int x = 0; x < w; x++)
            {
                if (dy == 1 || dy == 2)
                {
                    bool dot = (x % 4 < 2);
                    pixels[(allyDividerY + dy) * w + x] = dot ? accentBright : accentDark;
                }
                else
                {
                    pixels[(allyDividerY + dy) * w + x] = frameOuter;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void CreateTextures()
    {
        // Screen.width/heightがエディタで小さい値を返す場合に備えて最小値を設定
        int screenW = Mathf.Max(Screen.width, 800);
        int screenH = Mathf.Max(Screen.height, 600);

        // メイン背景(紫ベースのディザパターン)
        bgTexture = CreateDitheredBackground(
            screenW / dotScale, screenH / dotScale,
            GameColorScheme.PrimaryDark, GameColorScheme.PrimaryMid);

        // 味方ゾーン(青系グリッド)
        int allyHeight = Mathf.Max(1, (int)(screenH * allyZoneRatio / dotScale));
        allyZoneTexture = CreateZoneTexture(
            screenW / dotScale, allyHeight,
            GameColorScheme.AllyZoneDark, GameColorScheme.AllyZoneMid, true);

        // 敵ゾーン(赤紫系グリッド)
        int enemyHeight = Mathf.Max(1, (int)(screenH * enemyZoneRatio / dotScale));
        enemyZoneTexture = CreateZoneTexture(
            screenW / dotScale, enemyHeight,
            GameColorScheme.EnemyZoneDark, GameColorScheme.EnemyZoneMid, false);

        // ゾーン境界線
        dividerTexture = CreateDividerTexture(screenW / dotScale);
    }

    private Texture2D CreateDitheredBackground(int w, int h, Color c1, Color c2)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 2x2 ディザパターン
                bool dither = ((x + y) % 2 == 0);
                // 縦グラデーション
                float t = (float)y / h;
                Color baseColor = Color.Lerp(c1, c2, t * 0.3f);
                pixels[y * w + x] = dither ? baseColor : Color.Lerp(baseColor, c1, 0.3f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateZoneTexture(int w, int h, Color bgColor, Color gridColor, bool isAlly)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[w * h];
        int gridStep = gridSize / dotScale;
        if (gridStep < 2) gridStep = 2;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // グリッドパターン
                bool onGrid = (x % gridStep == 0) || (y % gridStep == 0);
                // 縦グラデーション(味方は上が明るく、敵は下が明るく)
                float t = isAlly ? (float)y / h : 1f - (float)y / h;
                Color base1 = Color.Lerp(bgColor, gridColor, t * 0.2f);

                if (onGrid)
                {
                    // グリッド線(少し明るく)
                    pixels[y * w + x] = Color.Lerp(base1, gridColor, 0.4f);
                }
                else
                {
                    // 背景(ディザ)
                    bool dither = ((x + y) % 2 == 0);
                    pixels[y * w + x] = dither ? base1 : Color.Lerp(base1, bgColor, 0.15f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateDividerTexture(int w)
    {
        int h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 黄色アクセントのドットライン
                if (y == 1 || y == 2)
                {
                    // 中央2列はアクセント色(ドット状)
                    bool dot = (x % 4 < 2);
                    pixels[y * w + x] = dot ? GameColorScheme.AccentBright : GameColorScheme.AccentDark;
                }
                else
                {
                    // 上下は紫枠
                    pixels[y * w + x] = GameColorScheme.FrameOuter;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void DestroyTextures()
    {
        if (bgTexture != null) Destroy(bgTexture);
        if (allyZoneTexture != null) Destroy(allyZoneTexture);
        if (enemyZoneTexture != null) Destroy(enemyZoneTexture);
        if (dividerTexture != null) Destroy(dividerTexture);
    }

    void Update()
    {
        // スクロール(将来的なアニメーション用)
        scrollOffset += Time.deltaTime * 10f;
        pulseTimer += Time.deltaTime;
    }

    void OnGUI()
    {
        // カメラ背景のみモード、またはSpriteRenderer使用時はスキップ
        if (cameraBackgroundOnly || !useOnGUI) return;

        // OnGUIはSpriteRendererより手前に描画されるため、
        // キャラクターが見えなくなる問題がある
        // GUI.depthは他のOnGUI要素との相対順序のみ制御
        GUI.depth = 100;

        // メイン背景
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTexture, ScaleMode.StretchToFill);

        // 敵ゾーン(上部)
        float enemyHeight = Screen.height * enemyZoneRatio;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, enemyHeight), enemyZoneTexture, ScaleMode.StretchToFill);

        // 敵ゾーン境界
        float enemyDividerY = enemyHeight - 4f * dotScale;
        GUI.DrawTexture(new Rect(0, enemyDividerY, Screen.width, 4f * dotScale), dividerTexture, ScaleMode.StretchToFill);

        // 味方ゾーン(下部)
        float allyHeight = Screen.height * allyZoneRatio;
        float allyY = Screen.height - allyHeight;
        GUI.DrawTexture(new Rect(0, allyY, Screen.width, allyHeight), allyZoneTexture, ScaleMode.StretchToFill);

        // 味方ゾーン境界
        GUI.DrawTexture(new Rect(0, allyY, Screen.width, 4f * dotScale), dividerTexture, ScaleMode.StretchToFill);
    }
}
