using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マップ画面 UI（8bitドット絵風）。
/// ピクセルフォントとレトロなノード表示でステージ選択を行う。
/// 華やかな演出（星雲、浮島、キラキラエフェクト）付き。
/// </summary>
public class WorldMapView : MonoBehaviour
{
    [Header("8bit カラー設定（GameColorScheme ベース）")]
    public Color bgColor1 = new Color(0.12f, 0.05f, 0.18f);     // PrimaryDark
    public Color bgColor2 = new Color(0.06f, 0.02f, 0.1f);      // より暗い紫
    public Color nodeColor = new Color(0.45f, 0.25f, 0.55f);    // PrimaryLight
    public Color nodeSelectColor = new Color(1f, 0.9f, 0.3f);   // AccentBright
    public Color nodeClearColor = new Color(0.4f, 0.85f, 0.5f); // HpFull(緑)
    public Color pathColor = new Color(0.5f, 0.35f, 0.6f);      // FrameOuter
    public Color textColor = new Color(0.95f, 0.92f, 1f);       // TextPrimary
    public Color headerColor = new Color(0.9f, 0.75f, 0.2f);    // AccentMid

    [Header("装飾カラー")]
    public Color nebulaColor1 = new Color(0.4f, 0.2f, 0.6f, 0.3f);   // 星雲色1
    public Color nebulaColor2 = new Color(0.2f, 0.3f, 0.5f, 0.25f);  // 星雲色2
    public Color sparkleColor = new Color(1f, 0.95f, 0.7f, 1f);      // キラキラ
    public Color islandColor = new Color(0.25f, 0.15f, 0.35f, 1f);   // 浮島
    public Color cloudColor = new Color(0.3f, 0.25f, 0.4f, 0.5f);    // 雲

    [Header("レイアウト")]
    public Vector2 nodeSize = new Vector2(48, 48);

    public event Action<StageData> StageSelected;
    public int CursorIndex { get; private set; } = 0;
    public bool IsLocked { get; private set; } = false;

    private Canvas canvas;
    private RectTransform panContainer;
    private List<NodeUi> nodes = new List<NodeUi>();
    private WorldMapData worldMap;
    private RunProgress runProgress;

    // 8bit用テクスチャ
    private Texture2D bgTexture;
    private Texture2D nodeTexture;
    private Texture2D nodeSelectTexture;
    private Texture2D nodeClearTexture;
    private Texture2D pathDotTexture;
    private Texture2D pathGlowTexture;
    private Texture2D starTexture;
    private Texture2D headerTexture;
    private Texture2D[] stageNameTextures;
    private Texture2D hudBoxTexture;
    private Texture2D controlHintTexture;

    // 装飾用テクスチャ
    private Texture2D[] nebulaTextures;
    private Texture2D[] sparkleTextures;
    private Texture2D[] islandTextures;
    private Texture2D[] cloudTextures;
    private Texture2D cornerTexture;
    private Texture2D selectionAuraTexture;

    private int pixelScale = 3;
    private float blinkTimer;
    private Coroutine idleRoutine;

    // スター背景
    private Vector2[] starPositions;
    private float[] starPhases;
    private int[] starSizes;

    // 星雲データ
    private Vector2[] nebulaPositions;
    private float[] nebulaPhases;
    private int[] nebulaTypes;

    // 浮島データ
    private Vector2[] islandPositions;
    private float[] islandPhases;
    private int[] islandTypes;

    // 雲データ
    private Vector2[] cloudPositions;
    private float[] cloudSpeeds;

    // キラキラデータ
    private Vector2[] sparklePositions;
    private float[] sparklePhases;
    private int[] sparkleTypes;

    public void Build(WorldMapData map, RunProgress progress)
    {
        Clear();
        worldMap = map;
        runProgress = progress;

        pixelScale = Mathf.Max(2, Screen.height / 360);

        CreateTextures();
        CreateDecorationTextures();
        InitializeStars();
        InitializeNebulae();
        InitializeIslands();
        InitializeClouds();
        InitializeSparkles();
        BuildCanvas();
        BuildNodes();
        CursorIndex = 0;
        ApplyHighlight();
        idleRoutine = StartCoroutine(IdleAnim());
    }

    public void Clear()
    {
        if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
        if (canvas != null) { Destroy(canvas.gameObject); canvas = null; }
        DestroyTextures();
        DestroyDecorationTextures();
        nodes.Clear();
        worldMap = null;
        runProgress = null;
        CursorIndex = 0;
        IsLocked = false;
    }

    private void CreateTextures()
    {
        // 背景
        bgTexture = PixelFontRenderer.CreateDitheredGradient(
            Screen.width / 4, Screen.height / 4, bgColor1, bgColor2, 2);

        // ノード（シンプルな四角）
        int nodePixelSize = (int)(nodeSize.x);
        nodeTexture = CreatePixelNode(nodePixelSize, nodeColor, false);
        nodeSelectTexture = CreatePixelNode(nodePixelSize, nodeSelectColor, true);
        nodeClearTexture = CreatePixelNode(nodePixelSize, nodeClearColor, false);

        // パスのドット
        pathDotTexture = PixelFontRenderer.CreatePixelBox(
            pixelScale * 3, pixelScale * 3,
            pathColor, pathColor, 0);

        // パスの光るドット（アニメーション用）
        pathGlowTexture = PixelFontRenderer.CreatePixelBox(
            pixelScale * 4, pixelScale * 4,
            nodeSelectColor, nodeSelectColor, 0);

        // スター（サイズバリエーション）
        starTexture = PixelFontRenderer.CreatePixelStar(pixelScale * 6, new Color(1f, 1f, 0.8f, 0.5f));

        // ヘッダー
        string headerText = worldMap != null ? worldMap.displayName.ToUpper() : "WORLD MAP";
        // 日本語は英語に変換（簡易）
        headerText = headerText.Replace("第一世界", "WORLD 1");
        headerText = headerText.Replace("第二世界", "WORLD 2");
        headerTexture = PixelFontRenderer.RenderText(headerText, headerColor, pixelScale + 1);

        // ステージ名
        if (worldMap != null && worldMap.stages != null)
        {
            stageNameTextures = new Texture2D[worldMap.stages.Count];
            for (int i = 0; i < worldMap.stages.Count; i++)
            {
                var entry = worldMap.stages[i];
                if (entry == null || entry.stage == null) continue;
                string name = $"STAGE {i + 1}";
                stageNameTextures[i] = PixelFontRenderer.RenderText(name, textColor, pixelScale);
            }
        }

        // HUDボックス
        hudBoxTexture = PixelFontRenderer.CreatePixelBox(
            200, 80,
            new Color(0.05f, 0.05f, 0.1f, 0.9f),
            new Color(0.3f, 0.4f, 0.6f),
            pixelScale);

        // 操作説明
        controlHintTexture = PixelFontRenderer.RenderText(
            "ARROW:SELECT  Z:OK  X:BACK", new Color(0.5f, 0.6f, 0.7f), pixelScale - 1);
    }

    private Texture2D CreatePixelNode(int size, Color color, bool animated)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        int border = pixelScale;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 角を丸くする（8bit風）
                bool isCorner = (x < border * 2 && y < border * 2) ||
                                (x < border * 2 && y >= size - border * 2) ||
                                (x >= size - border * 2 && y < border * 2) ||
                                (x >= size - border * 2 && y >= size - border * 2);

                bool isBorder = x < border || x >= size - border ||
                                y < border || y >= size - border;

                if (isCorner && (x < border || y < border || x >= size - border || y >= size - border))
                {
                    pixels[y * size + x] = Color.clear;
                }
                else if (isBorder)
                {
                    pixels[y * size + x] = color;
                }
                else
                {
                    // 内側は暗め
                    pixels[y * size + x] = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 1f);
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
        if (nodeTexture != null) Destroy(nodeTexture);
        if (nodeSelectTexture != null) Destroy(nodeSelectTexture);
        if (nodeClearTexture != null) Destroy(nodeClearTexture);
        if (pathDotTexture != null) Destroy(pathDotTexture);
        if (pathGlowTexture != null) Destroy(pathGlowTexture);
        if (starTexture != null) Destroy(starTexture);
        if (headerTexture != null) Destroy(headerTexture);
        if (hudBoxTexture != null) Destroy(hudBoxTexture);
        if (controlHintTexture != null) Destroy(controlHintTexture);

        if (stageNameTextures != null)
        {
            foreach (var tex in stageNameTextures)
                if (tex != null) Destroy(tex);
        }
    }

    private void CreateDecorationTextures()
    {
        // 星雲テクスチャ（複数パターン）
        nebulaTextures = new Texture2D[3];
        nebulaTextures[0] = CreateNebulaTexture(80, 60, nebulaColor1);
        nebulaTextures[1] = CreateNebulaTexture(100, 80, nebulaColor2);
        nebulaTextures[2] = CreateNebulaTexture(60, 50, new Color(0.5f, 0.25f, 0.4f, 0.2f));

        // キラキラテクスチャ
        sparkleTextures = new Texture2D[3];
        sparkleTextures[0] = CreateSparkleTexture(pixelScale * 4, sparkleColor);
        sparkleTextures[1] = CreateSparkleTexture(pixelScale * 6, sparkleColor * 0.8f);
        sparkleTextures[2] = CreateSparkleTexture(pixelScale * 3, new Color(1f, 0.8f, 0.4f, 1f));

        // 浮島テクスチャ
        islandTextures = new Texture2D[3];
        islandTextures[0] = CreateIslandTexture(48, 32, islandColor);
        islandTextures[1] = CreateIslandTexture(64, 40, islandColor * 0.9f);
        islandTextures[2] = CreateIslandTexture(36, 24, islandColor * 1.1f);

        // 雲テクスチャ
        cloudTextures = new Texture2D[2];
        cloudTextures[0] = CreateCloudTexture(80, 24, cloudColor);
        cloudTextures[1] = CreateCloudTexture(120, 32, cloudColor * 0.8f);

        // コーナー装飾
        cornerTexture = CreateCornerTexture();

        // 選択オーラ
        selectionAuraTexture = CreateAuraTexture(80, nodeSelectColor);
    }

    private Texture2D CreateNebulaTexture(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        System.Random rng = new System.Random(width * height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 中心からの距離で減衰
                float cx = (float)x / width - 0.5f;
                float cy = (float)y / height - 0.5f;
                float dist = Mathf.Sqrt(cx * cx + cy * cy) * 2f;
                float fade = Mathf.Max(0, 1f - dist);

                // ディザリングパターン
                bool dither = (x + y) % 2 == 0;
                float alpha = fade * (dither ? 1f : 0.6f) * color.a;

                // ランダムで穴を開ける
                if (rng.NextDouble() < 0.3f) alpha *= 0.5f;

                pixels[y * width + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateSparkleTexture(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[size * size];
        int center = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 十字と対角の輝き
                bool isCross = (x == center || y == center);
                bool isDiag = (Mathf.Abs(x - center) == Mathf.Abs(y - center));
                bool isCenter = (x == center && y == center);

                if (isCenter)
                {
                    pixels[y * size + x] = color;
                }
                else if (isCross)
                {
                    float dist = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    float fade = 1f - dist / center;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * fade);
                }
                else if (isDiag && Mathf.Abs(x - center) <= center / 2)
                {
                    float dist = Mathf.Abs(x - center);
                    float fade = 1f - dist / (center / 2f);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * fade * 0.5f);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateIslandTexture(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width * height];
        Color topColor = color * 1.3f;
        Color sideColor = color * 0.7f;
        Color bottomColor = new Color(0.15f, 0.1f, 0.2f, 0.8f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 楕円形の島
                float cx = (float)x / width - 0.5f;
                float cy = (float)y / height - 0.5f;
                float ellipse = (cx * cx) / 0.2f + (cy * cy) / 0.15f;

                if (ellipse > 1f)
                {
                    pixels[y * width + x] = Color.clear;
                }
                else
                {
                    // 上部は明るく、下部は暗く
                    float fy = (float)y / height;
                    if (fy < 0.3f)
                    {
                        pixels[y * width + x] = topColor;
                    }
                    else if (fy < 0.6f)
                    {
                        pixels[y * width + x] = color;
                    }
                    else if (fy < 0.8f)
                    {
                        pixels[y * width + x] = sideColor;
                    }
                    else
                    {
                        pixels[y * width + x] = bottomColor;
                    }

                    // ピクセル風のディテール
                    if ((x + y) % 4 == 0 && fy < 0.4f)
                    {
                        pixels[y * width + x] = topColor * 1.2f;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateCloudTexture(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width * height];
        System.Random rng = new System.Random(width + height);

        // 雲のベース（複数の楕円の重ね合わせ）
        float[] blobX = { 0.2f, 0.4f, 0.6f, 0.8f };
        float[] blobSize = { 0.15f, 0.2f, 0.18f, 0.12f };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float fx = (float)x / width;
                float fy = (float)y / height;

                float alpha = 0f;
                for (int i = 0; i < blobX.Length; i++)
                {
                    float dx = fx - blobX[i];
                    float dy = fy - 0.5f;
                    float dist = Mathf.Sqrt(dx * dx / (blobSize[i] * blobSize[i]) + dy * dy / 0.2f);
                    if (dist < 1f)
                    {
                        alpha = Mathf.Max(alpha, 1f - dist);
                    }
                }

                // ディザリング
                bool dither = (x + y) % 2 == 0;
                alpha *= dither ? 1f : 0.7f;

                pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateCornerTexture()
    {
        int size = pixelScale * 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[size * size];
        Color outer = headerColor;
        Color inner = nodeSelectColor * 0.6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int border = pixelScale * 2;
                bool isL = x < border * 3 || y < border * 3;

                if (isL)
                {
                    if (x < border || y < border)
                    {
                        pixels[y * size + x] = outer;
                    }
                    else if (x < border * 2 || y < border * 2)
                    {
                        pixels[y * size + x] = inner;
                    }
                    else
                    {
                        pixels[y * size + x] = outer * 0.5f;
                    }
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateAuraTexture(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[size * size];
        int center = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float normalizedDist = dist / center;

                // リング状のオーラ
                float ringDist = Mathf.Abs(normalizedDist - 0.7f);
                float alpha = Mathf.Max(0, 0.3f - ringDist);

                // ディザリング
                if ((x + y) % 2 == 1) alpha *= 0.5f;

                pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void DestroyDecorationTextures()
    {
        if (nebulaTextures != null)
        {
            foreach (var tex in nebulaTextures) if (tex != null) Destroy(tex);
        }
        if (sparkleTextures != null)
        {
            foreach (var tex in sparkleTextures) if (tex != null) Destroy(tex);
        }
        if (islandTextures != null)
        {
            foreach (var tex in islandTextures) if (tex != null) Destroy(tex);
        }
        if (cloudTextures != null)
        {
            foreach (var tex in cloudTextures) if (tex != null) Destroy(tex);
        }
        if (cornerTexture != null) Destroy(cornerTexture);
        if (selectionAuraTexture != null) Destroy(selectionAuraTexture);
    }

    private void InitializeStars()
    {
        int starCount = 60;
        starPositions = new Vector2[starCount];
        starPhases = new float[starCount];
        starSizes = new int[starCount];

        System.Random rng = new System.Random(123);
        for (int i = 0; i < starCount; i++)
        {
            starPositions[i] = new Vector2(
                (float)rng.NextDouble(),
                (float)rng.NextDouble()
            );
            starPhases[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            starSizes[i] = rng.Next(3) + 1; // 1-3
        }
    }

    private void InitializeNebulae()
    {
        int nebulaCount = 5;
        nebulaPositions = new Vector2[nebulaCount];
        nebulaPhases = new float[nebulaCount];
        nebulaTypes = new int[nebulaCount];

        System.Random rng = new System.Random(456);
        for (int i = 0; i < nebulaCount; i++)
        {
            nebulaPositions[i] = new Vector2(
                (float)rng.NextDouble(),
                (float)rng.NextDouble()
            );
            nebulaPhases[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            nebulaTypes[i] = rng.Next(3);
        }
    }

    private void InitializeIslands()
    {
        int islandCount = 4;
        islandPositions = new Vector2[islandCount];
        islandPhases = new float[islandCount];
        islandTypes = new int[islandCount];

        System.Random rng = new System.Random(789);
        for (int i = 0; i < islandCount; i++)
        {
            // 画面の端の方に配置
            float x = (float)rng.NextDouble();
            if (x > 0.3f && x < 0.7f) x = x < 0.5f ? 0.15f : 0.85f;

            islandPositions[i] = new Vector2(x, (float)rng.NextDouble() * 0.6f + 0.2f);
            islandPhases[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            islandTypes[i] = rng.Next(3);
        }
    }

    private void InitializeClouds()
    {
        int cloudCount = 6;
        cloudPositions = new Vector2[cloudCount];
        cloudSpeeds = new float[cloudCount];

        System.Random rng = new System.Random(321);
        for (int i = 0; i < cloudCount; i++)
        {
            cloudPositions[i] = new Vector2(
                (float)rng.NextDouble(),
                (float)rng.NextDouble() * 0.3f + 0.1f // 上部に配置
            );
            cloudSpeeds[i] = 0.01f + (float)rng.NextDouble() * 0.02f;
        }
    }

    private void InitializeSparkles()
    {
        int sparkleCount = 15;
        sparklePositions = new Vector2[sparkleCount];
        sparklePhases = new float[sparkleCount];
        sparkleTypes = new int[sparkleCount];

        System.Random rng = new System.Random(654);
        for (int i = 0; i < sparkleCount; i++)
        {
            sparklePositions[i] = new Vector2(
                (float)rng.NextDouble(),
                (float)rng.NextDouble()
            );
            sparklePhases[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            sparkleTypes[i] = rng.Next(3);
        }
    }

    public void MoveCursor(int delta)
    {
        if (IsLocked || nodes.Count == 0) return;
        CursorIndex = Mathf.Clamp(CursorIndex + delta, 0, nodes.Count - 1);
        ApplyHighlight();
    }

    public void Confirm()
    {
        if (IsLocked || nodes.Count == 0) return;
        if (CursorIndex < 0 || CursorIndex >= nodes.Count) return;
        var stage = nodes[CursorIndex].stage;
        if (stage == null) return;
        IsLocked = true;
        StartCoroutine(ConfirmFlash(stage));
    }

    private IEnumerator ConfirmFlash(StageData stage)
    {
        // 選択フラッシュ
        for (int i = 0; i < 6; i++)
        {
            nodes[CursorIndex].visible = !nodes[CursorIndex].visible;
            yield return new WaitForSeconds(0.08f);
        }
        nodes[CursorIndex].visible = true;
        yield return new WaitForSeconds(0.2f);

        StageSelected?.Invoke(stage);
    }

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("WorldMapCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildNodes()
    {
        if (worldMap == null) return;
        for (int i = 0; i < worldMap.stages.Count; i++)
        {
            var entry = worldMap.stages[i];
            if (entry == null || entry.stage == null) continue;

            bool cleared = runProgress != null && runProgress.clearedStageIds.Contains(entry.stage.id);

            nodes.Add(new NodeUi
            {
                anchor = entry.nodePosition,
                stage = entry.stage,
                cleared = cleared,
                visible = true,
            });
        }
    }

    private void ApplyHighlight()
    {
        // 更新のみ（描画はOnGUIで行う）
    }

    private IEnumerator IdleAnim()
    {
        while (true)
        {
            blinkTimer += Time.deltaTime;
            yield return null;
        }
    }

    private void OnGUI()
    {
        if (worldMap == null) return;

        DrawBackground();
        DrawNebulae();
        DrawClouds();
        DrawStars();
        DrawIslands();
        DrawSparkles();
        DrawPaths();
        DrawNodes();
        DrawCorners();
        DrawHeader();
        DrawHUD();
        DrawControlHints();
    }

    private void DrawBackground()
    {
        if (bgTexture == null) return;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTexture, ScaleMode.StretchToFill);
    }

    private void DrawStars()
    {
        if (starTexture == null || starPositions == null) return;

        for (int i = 0; i < starPositions.Length; i++)
        {
            float twinkle = Mathf.Sin(blinkTimer * 2f + starPhases[i]);
            if (twinkle < 0.2f) continue;

            float alpha = 0.3f + twinkle * 0.4f;
            Color c = GUI.color;

            // サイズによって色も変える
            Color starCol = starSizes[i] switch
            {
                1 => new Color(1f, 1f, 0.9f, alpha),
                2 => new Color(1f, 0.95f, 0.7f, alpha),
                _ => new Color(1f, 0.85f, 0.5f, alpha)
            };
            GUI.color = starCol;

            float x = starPositions[i].x * Screen.width;
            float y = starPositions[i].y * Screen.height;

            float size = starTexture.width * (0.5f + starSizes[i] * 0.25f);
            GUI.DrawTexture(new Rect(x, y, size, size), starTexture);
            GUI.color = c;
        }
    }

    private void DrawNebulae()
    {
        if (nebulaTextures == null || nebulaPositions == null) return;

        for (int i = 0; i < nebulaPositions.Length; i++)
        {
            int type = nebulaTypes[i];
            if (type >= nebulaTextures.Length || nebulaTextures[type] == null) continue;

            var tex = nebulaTextures[type];

            // ゆっくり脈動
            float pulse = 0.7f + 0.3f * Mathf.Sin(blinkTimer * 0.5f + nebulaPhases[i]);
            Color c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, pulse);

            float x = nebulaPositions[i].x * Screen.width - tex.width;
            float y = nebulaPositions[i].y * Screen.height - tex.height;

            // 少しゆらゆら
            x += Mathf.Sin(blinkTimer * 0.3f + nebulaPhases[i]) * 5f;
            y += Mathf.Cos(blinkTimer * 0.2f + nebulaPhases[i]) * 3f;

            GUI.DrawTexture(new Rect(x, y, tex.width * 2, tex.height * 2), tex);
            GUI.color = c;
        }
    }

    private void DrawIslands()
    {
        if (islandTextures == null || islandPositions == null) return;

        for (int i = 0; i < islandPositions.Length; i++)
        {
            int type = islandTypes[i];
            if (type >= islandTextures.Length || islandTextures[type] == null) continue;

            var tex = islandTextures[type];

            // 上下にふわふわ
            float bob = Mathf.Sin(blinkTimer * 0.8f + islandPhases[i]) * pixelScale * 2;

            float x = islandPositions[i].x * Screen.width - tex.width / 2f;
            float y = islandPositions[i].y * Screen.height - tex.height / 2f + bob;

            GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
        }
    }

    private void DrawClouds()
    {
        if (cloudTextures == null || cloudPositions == null) return;

        for (int i = 0; i < cloudPositions.Length; i++)
        {
            int type = i % cloudTextures.Length;
            if (cloudTextures[type] == null) continue;

            var tex = cloudTextures[type];

            // 横にゆっくり流れる
            float xPos = cloudPositions[i].x + blinkTimer * cloudSpeeds[i];
            xPos = xPos % 1.2f - 0.1f; // ループ

            float x = xPos * Screen.width;
            float y = cloudPositions[i].y * Screen.height;

            Color c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
            GUI.color = c;
        }
    }

    private void DrawSparkles()
    {
        if (sparkleTextures == null || sparklePositions == null) return;

        for (int i = 0; i < sparklePositions.Length; i++)
        {
            // キラキラは周期的に現れて消える
            float cycle = Mathf.Sin(blinkTimer * 3f + sparklePhases[i]);
            if (cycle < 0.5f) continue;

            int type = sparkleTypes[i];
            if (type >= sparkleTextures.Length || sparkleTextures[type] == null) continue;

            var tex = sparkleTextures[type];

            float alpha = (cycle - 0.5f) * 2f; // 0-1
            Color c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            float x = sparklePositions[i].x * Screen.width - tex.width / 2f;
            float y = sparklePositions[i].y * Screen.height - tex.height / 2f;

            // 回転風のスケール変化
            float scaleX = 1f + Mathf.Sin(blinkTimer * 5f + sparklePhases[i]) * 0.3f;
            float scaleY = 1f + Mathf.Cos(blinkTimer * 5f + sparklePhases[i]) * 0.3f;

            GUI.DrawTexture(new Rect(x, y, tex.width * scaleX, tex.height * scaleY), tex);
            GUI.color = c;
        }
    }

    private void DrawCorners()
    {
        if (cornerTexture == null) return;

        float cs = cornerTexture.width;
        float margin = pixelScale * 4;

        var matrix = GUI.matrix;

        // 左上
        GUI.DrawTexture(new Rect(margin, margin, cs, cs), cornerTexture);

        // 右上（水平反転）
        GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), new Vector2(Screen.width - margin, margin + cs / 2f));
        GUI.DrawTexture(new Rect(Screen.width - margin - cs, margin, cs, cs), cornerTexture);
        GUI.matrix = matrix;

        // 左下（垂直反転）
        GUIUtility.ScaleAroundPivot(new Vector2(1f, -1f), new Vector2(margin + cs / 2f, Screen.height - margin));
        GUI.DrawTexture(new Rect(margin, Screen.height - margin - cs, cs, cs), cornerTexture);
        GUI.matrix = matrix;

        // 右下（両方反転）
        GUIUtility.ScaleAroundPivot(new Vector2(-1f, -1f), new Vector2(Screen.width - margin, Screen.height - margin));
        GUI.DrawTexture(new Rect(Screen.width - margin - cs, Screen.height - margin - cs, cs, cs), cornerTexture);
        GUI.matrix = matrix;
    }

    private void DrawPaths()
    {
        if (pathDotTexture == null || nodes.Count < 2) return;

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector2 from = nodes[i].anchor;
            Vector2 to = nodes[i + 1].anchor;

            // ドットで線を描画
            int dotCount = 16;
            for (int d = 1; d < dotCount; d++)
            {
                float t = (float)d / dotCount;
                float x = Mathf.Lerp(from.x, to.x, t) * Screen.width;
                float y = (1f - Mathf.Lerp(from.y, to.y, t)) * Screen.height;

                // 少し波打たせる
                y += Mathf.Sin(t * Mathf.PI) * pixelScale * 3;

                // 通常のドット
                GUI.DrawTexture(new Rect(
                    x - pathDotTexture.width / 2f,
                    y - pathDotTexture.height / 2f,
                    pathDotTexture.width,
                    pathDotTexture.height), pathDotTexture);
            }

            // 光るドットのアニメーション（選択中のパスは光が流れる）
            if (pathGlowTexture != null && (i == CursorIndex || i == CursorIndex - 1))
            {
                // パス上を移動する光
                float glowT = (blinkTimer * 0.5f) % 1f;
                if (i == CursorIndex - 1) glowT = 1f - glowT; // 逆方向

                float glowX = Mathf.Lerp(from.x, to.x, glowT) * Screen.width;
                float glowY = (1f - Mathf.Lerp(from.y, to.y, glowT)) * Screen.height;
                glowY += Mathf.Sin(glowT * Mathf.PI) * pixelScale * 3;

                Color c = GUI.color;
                float alpha = 0.5f + 0.5f * Mathf.Sin(blinkTimer * 8f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(
                    glowX - pathGlowTexture.width / 2f,
                    glowY - pathGlowTexture.height / 2f,
                    pathGlowTexture.width,
                    pathGlowTexture.height), pathGlowTexture);
                GUI.color = c;
            }
        }
    }

    private void DrawNodes()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (!node.visible) continue;

            bool isSelected = i == CursorIndex;
            Texture2D tex = isSelected ? nodeSelectTexture :
                            node.cleared ? nodeClearTexture : nodeTexture;
            if (tex == null) continue;

            float x = node.anchor.x * Screen.width;
            float y = (1f - node.anchor.y) * Screen.height;

            // 選択中は脈動
            float scale = 1f;
            if (isSelected)
            {
                scale = 1f + Mathf.Sin(blinkTimer * 5f) * 0.1f;

                // 選択オーラを描画
                if (selectionAuraTexture != null)
                {
                    float auraScale = 1.2f + Mathf.Sin(blinkTimer * 3f) * 0.2f;
                    float auraSize = selectionAuraTexture.width * auraScale;
                    Color c = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.5f + Mathf.Sin(blinkTimer * 4f) * 0.3f);
                    GUI.DrawTexture(new Rect(
                        x - auraSize / 2f,
                        y - auraSize / 2f,
                        auraSize, auraSize), selectionAuraTexture);
                    GUI.color = c;
                }
            }

            float w = tex.width * scale;
            float h = tex.height * scale;

            GUI.DrawTexture(new Rect(x - w / 2f, y - h / 2f, w, h), tex);

            // ステージ番号
            Texture2D numTex = PixelFontRenderer.RenderText((i + 1).ToString(),
                isSelected ? nodeSelectColor : textColor, pixelScale);
            if (numTex != null)
            {
                GUI.DrawTexture(new Rect(
                    x - numTex.width / 2f,
                    y - numTex.height / 2f,
                    numTex.width,
                    numTex.height), numTex);
            }

            // ステージ名（選択中のみ）- 背景付き
            if (isSelected && stageNameTextures != null && i < stageNameTextures.Length)
            {
                Texture2D nameTex = stageNameTextures[i];
                if (nameTex != null)
                {
                    float nameY = y + tex.height / 2f + pixelScale * 4;

                    // 名前の背景
                    float bgPadding = pixelScale * 2;
                    Texture2D nameBg = PixelFontRenderer.CreatePixelBox(
                        (int)(nameTex.width + bgPadding * 2),
                        (int)(nameTex.height + bgPadding * 2),
                        new Color(0.02f, 0.02f, 0.05f, 0.85f),
                        nodeSelectColor * 0.5f, 1);
                    GUI.DrawTexture(new Rect(
                        x - nameTex.width / 2f - bgPadding,
                        nameY - bgPadding,
                        nameTex.width + bgPadding * 2,
                        nameTex.height + bgPadding * 2), nameBg);
                    Destroy(nameBg);

                    GUI.DrawTexture(new Rect(
                        x - nameTex.width / 2f,
                        nameY,
                        nameTex.width,
                        nameTex.height), nameTex);
                }
            }

            // クリア済みマーク（星マーク）
            if (node.cleared)
            {
                Texture2D clearStar = PixelFontRenderer.CreatePixelStar(pixelScale * 8, nodeClearColor);
                if (clearStar != null)
                {
                    // 回転アニメーション風の脈動
                    float starScale = 1f + Mathf.Sin(blinkTimer * 2f + i) * 0.15f;
                    float sw = clearStar.width * starScale;
                    float sh = clearStar.height * starScale;
                    GUI.DrawTexture(new Rect(
                        x + tex.width / 2f - sw / 2f,
                        y - tex.height / 2f - sh / 2f,
                        sw, sh), clearStar);
                    Destroy(clearStar);
                }
            }
        }
    }

    private void DrawHeader()
    {
        if (headerTexture == null) return;

        float x = (Screen.width - headerTexture.width) / 2f;
        float y = pixelScale * 12;

        // ヘッダー背景（豪華に）
        float bgWidth = headerTexture.width + pixelScale * 24;
        float bgHeight = headerTexture.height + pixelScale * 12;
        Texture2D headerBg = PixelFontRenderer.CreatePixelBox(
            (int)bgWidth, (int)bgHeight,
            new Color(0.02f, 0.02f, 0.05f, 0.9f),
            headerColor, pixelScale);

        float bgX = (Screen.width - bgWidth) / 2f;
        float bgY = y - pixelScale * 6;
        GUI.DrawTexture(new Rect(bgX, bgY, bgWidth, bgHeight), headerBg);

        // 内側の装飾ライン
        Texture2D innerLine = PixelFontRenderer.CreatePixelBox(
            (int)(bgWidth - pixelScale * 8), (int)(bgHeight - pixelScale * 6),
            Color.clear, headerColor * 0.4f, 1);
        GUI.DrawTexture(new Rect(
            bgX + pixelScale * 4, bgY + pixelScale * 3,
            bgWidth - pixelScale * 8, bgHeight - pixelScale * 6), innerLine);
        Destroy(innerLine);

        // タイトルに揺れアニメーション
        float bobX = Mathf.Sin(blinkTimer * 1.5f) * pixelScale;
        GUI.DrawTexture(new Rect(x + bobX, y, headerTexture.width, headerTexture.height), headerTexture);

        // 両サイドに星装飾
        if (starTexture != null)
        {
            float starY = y + headerTexture.height / 2f - starTexture.height / 2f;
            float starPulse = 0.7f + 0.3f * Mathf.Sin(blinkTimer * 3f);
            Color c = GUI.color;
            GUI.color = new Color(1f, 0.9f, 0.3f, starPulse);
            GUI.DrawTexture(new Rect(bgX + pixelScale * 4, starY, starTexture.width, starTexture.height), starTexture);
            GUI.DrawTexture(new Rect(bgX + bgWidth - pixelScale * 4 - starTexture.width, starY, starTexture.width, starTexture.height), starTexture);
            GUI.color = c;
        }

        Destroy(headerBg);
    }

    private void DrawHUD()
    {
        if (runProgress == null) return;

        float x = pixelScale * 12;
        float y = Screen.height - pixelScale * 60;

        // HUD背景（より装飾的に）
        int hudW = 180;
        int hudH = 70;
        Texture2D hud = PixelFontRenderer.CreatePixelBox(
            hudW, hudH,
            new Color(0.03f, 0.03f, 0.08f, 0.92f),
            new Color(0.4f, 0.5f, 0.7f), pixelScale);
        GUI.DrawTexture(new Rect(x, y, hudW, hudH), hud);
        Destroy(hud);

        // 内側装飾
        Texture2D hudInner = PixelFontRenderer.CreatePixelBox(
            hudW - pixelScale * 6, hudH - pixelScale * 6,
            Color.clear, new Color(0.3f, 0.35f, 0.5f, 0.5f), 1);
        GUI.DrawTexture(new Rect(x + pixelScale * 3, y + pixelScale * 3, hudW - pixelScale * 6, hudH - pixelScale * 6), hudInner);
        Destroy(hudInner);

        // ラベル（アイコン風）
        float labelX = x + pixelScale * 6;
        float labelY = y + pixelScale * 8;

        // EXP
        Texture2D expIcon = PixelFontRenderer.CreatePixelStar(pixelScale * 6, new Color(0.4f, 0.7f, 1f));
        GUI.DrawTexture(new Rect(labelX, labelY, expIcon.width, expIcon.height), expIcon);
        Destroy(expIcon);

        Texture2D expTex = PixelFontRenderer.RenderText(
            $"{runProgress.accumulatedExp}", new Color(0.6f, 0.85f, 1f), pixelScale);
        if (expTex != null)
        {
            GUI.DrawTexture(new Rect(
                labelX + pixelScale * 8, labelY,
                expTex.width, expTex.height), expTex);
        }

        // BOMB
        float bombY = labelY + pixelScale * 12;
        Texture2D bombIcon = PixelFontRenderer.CreatePixelStar(pixelScale * 6, new Color(1f, 0.6f, 0.3f));
        GUI.DrawTexture(new Rect(labelX, bombY, bombIcon.width, bombIcon.height), bombIcon);
        Destroy(bombIcon);

        Texture2D bombTex = PixelFontRenderer.RenderText(
            $"+{runProgress.accumulatedBombs}", new Color(1f, 0.8f, 0.5f), pixelScale);
        if (bombTex != null)
        {
            GUI.DrawTexture(new Rect(
                labelX + pixelScale * 8, bombY,
                bombTex.width, bombTex.height), bombTex);
        }
    }

    private void DrawControlHints()
    {
        if (controlHintTexture == null) return;

        float y = Screen.height - controlHintTexture.height - pixelScale * 8;
        float x = Screen.width - controlHintTexture.width - pixelScale * 12;

        // 背景
        float bgPad = pixelScale * 3;
        Texture2D hintBg = PixelFontRenderer.CreatePixelBox(
            (int)(controlHintTexture.width + bgPad * 2),
            (int)(controlHintTexture.height + bgPad * 2),
            new Color(0.02f, 0.02f, 0.05f, 0.8f),
            new Color(0.4f, 0.45f, 0.55f, 0.6f), 1);
        GUI.DrawTexture(new Rect(x - bgPad, y - bgPad,
            controlHintTexture.width + bgPad * 2,
            controlHintTexture.height + bgPad * 2), hintBg);
        Destroy(hintBg);

        GUI.DrawTexture(new Rect(x, y, controlHintTexture.width, controlHintTexture.height), controlHintTexture);
    }

    private void OnDestroy()
    {
        Clear();
    }

    private class NodeUi
    {
        public Vector2 anchor;
        public StageData stage;
        public bool cleared;
        public bool visible;
    }
}
