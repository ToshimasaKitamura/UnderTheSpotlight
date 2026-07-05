using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 部位選択 UI。
/// シーンに最初から配置されている Enemy GameObject(SpriteRenderer 持ち)に
/// **直接 world-space で赤いヒットボックスを重ねる**。
/// 重複した白いパネルや placeholder 画像は作らない(既に画面に出ているキャラを使う)。
///
/// 別途、画面 overlay の Canvas には:
///   - 上部に「Nさん [カード名] → 部位選択」ヘッダ
///   - 下部に部位チップ一覧(名前 + HP)
///   - 下部にタイマー秒数 + 緑のプログレスバー
///   - 一番下に操作ヒント
/// を出す。これらは敵スプライトを隠さない位置に配置。
/// テキスト描画は PixelFontRenderer + IMGUI を使用。
///
/// 入力: ↑↓ or ←→ で選択、Enter/Space/Z で確定。タイマー切れで自動確定。
/// </summary>
public class PartSelectView : MonoBehaviour
{
    [Header("色")]
    [Tooltip("ヒットボックス枠の色(通常時、塗りなし)。")]
    public Color hitboxColor = new Color(0.98f, 0.15f, 0.15f, 1f);
    [Tooltip("ヒットボックス枠の色(選択中)。")]
    public Color hitboxSelectedColor = new Color(1f, 0.92f, 0.15f, 1f);
    [Tooltip("プログレスバーの色(緑)。")]
    public Color progressBarColor = new Color(0.4f, 0.95f, 0.45f, 1f);

    [Header("ヒットボックスの枠")]
    [Tooltip("枠線の太さ(敵スプライト短辺に対する比率)。0.02 でだいたい 2% 厚。")]
    [Range(0.005f, 0.1f)] public float hitboxBorderRatio = 0.025f;
    [Tooltip("敵スプライトより前に出すための sortingOrder 加算。")]
    public int hitboxSortingOrderOffset = 10;

    public event Action<int> PartConfirmed;
    public int CursorIndex { get; private set; } = 0;
    public bool IsLocked { get; private set; } = false;

    private Canvas overlayCanvas;
    private Transform hitboxParent; // 敵 Visual の transform(SpriteRenderer ヒットボックスの親)
    private RectTransform progressFill;
    private float progressBarWidth;

    private float timeLimit;
    private float timeLeft;

    private List<EnemyPart> currentParts;
    private List<HitboxUi> hitboxes = new List<HitboxUi>();
    private List<PartChipUi> chips = new List<PartChipUi>();
    private EnemyRuntimeState runtimeState;
    private Coroutine pulseRoutine;

    // IMGUI用
    private PartSelectIMGUIHelper guiHelper;
    private string headerText = "部位を選んで！";
    private Color headerColor = Color.white;
    private string timerText = "";
    private string[] chipTexts;
    private Color[] chipColors;
    private bool[] chipSelected;

    /// <summary>
    /// 構築。<paramref name="enemyVisual"/> は シーン上の Enemy GameObject の Visual 子(SpriteRenderer を持つ Transform)。
    /// この transform の子として world-space ヒットボックスを生成する → 既存の浮遊モーションにも追従。
    /// </summary>
    public void Build(IList<EnemyPart> parts, EnemyRuntimeState state, Transform enemyVisual)
    {
        Clear();
        currentParts = new List<EnemyPart>(parts);
        runtimeState = state;

        // チップ用配列初期化
        chipTexts = new string[parts.Count];
        chipColors = new Color[parts.Count];
        chipSelected = new bool[parts.Count];

        BuildOverlayCanvas();
        BuildHitboxesOnEnemy(enemyVisual);
        BuildChips();
        BuildTimer();
        CursorIndex = 0;
        ApplyHighlight();
        pulseRoutine = StartCoroutine(PulseRoutine());

        // IMGUIヘルパー初期化
        guiHelper = gameObject.AddComponent<PartSelectIMGUIHelper>();
        guiHelper.Initialize(this);
    }

    public void SetHeader(string text, Color tint)
    {
        headerText = text;
        headerColor = tint;
        if (guiHelper != null) guiHelper.MarkDirty();
    }

    // IMGUI用アクセサ
    public string GetHeaderText() => headerText;
    public Color GetHeaderColor() => headerColor;
    public string GetTimerText() => timerText;
    public string[] GetChipTexts() => chipTexts;
    public Color[] GetChipColors() => chipColors;
    public bool[] GetChipSelected() => chipSelected;
    public int GetChipCount() => chipTexts != null ? chipTexts.Length : 0;

    /// <summary>互換用 no-op (セリフは DodgePhase で表示)。</summary>
    public void SetEnemySpeech(string text) { /* moved to DodgePhase */ }

    public void SetTimeLimit(float seconds)
    {
        timeLimit = seconds;
        timeLeft = seconds;
        UpdateTimerVisual();
    }

    public void MoveCursor(int delta)
    {
        if (IsLocked || hitboxes.Count == 0) return;
        CursorIndex = Mathf.Clamp(CursorIndex + delta, 0, hitboxes.Count - 1);
        ApplyHighlight();
    }

    public void Confirm()
    {
        if (IsLocked || hitboxes.Count == 0) return;
        IsLocked = true;
        PartConfirmed?.Invoke(CursorIndex);
    }

    public void Tick(float dt)
    {
        if (IsLocked) return;
        if (timeLimit > 0f)
        {
            timeLeft -= dt;
            if (timeLeft <= 0f) { Confirm(); return; }
            UpdateTimerVisual();
        }
    }

    private void UpdateTimerVisual()
    {
        timerText = timeLimit > 0f ? $"残り {Mathf.Max(0f, timeLeft):F1} 秒" : "";
        if (guiHelper != null) guiHelper.MarkDirty();
        if (progressFill != null && timeLimit > 0f)
        {
            float p = Mathf.Clamp01(timeLeft / timeLimit);
            progressFill.sizeDelta = new Vector2(progressBarWidth * p, progressFill.sizeDelta.y);
        }
    }

    public void Clear()
    {
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
        // 敵の transform に貼った SpriteRenderer 群を個別に破棄
        for (int i = 0; i < hitboxes.Count; i++)
        {
            if (hitboxes[i].go != null) Destroy(hitboxes[i].go);
        }
        hitboxes.Clear();
        chips.Clear();
        currentParts = null;
        if (overlayCanvas != null) { Destroy(overlayCanvas.gameObject); overlayCanvas = null; }
        if (guiHelper != null) { Destroy(guiHelper); guiHelper = null; }
        hitboxParent = null;
        progressFill = null;
        chipTexts = null;
        chipColors = null;
        chipSelected = null;
        headerText = "部位を選んで！";
        timerText = "";
        CursorIndex = 0;
        IsLocked = false;
    }

    // ----- 構築 -----
    private void BuildOverlayCanvas()
    {
        var canvasObj = new GameObject("PartSelectOverlay",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        overlayCanvas = canvasObj.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 120;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // テキストはIMGUIで描画するため、ここではCanvasのみ作成
    }

    /// <summary>
    /// 既存の敵 Visual transform の子に **枠線4本(SpriteRenderer)**で赤い矩形枠を直接配置する。
    /// 中は塗りなし(キャラ絵が見える)。Visual の浮遊にも自動追従。
    /// 位置・サイズは sprite.bounds に対する正規化座標(hitboxCenter / hitboxSize)で計算。
    /// </summary>
    private void BuildHitboxesOnEnemy(Transform enemyVisual)
    {
        if (enemyVisual == null)
        {
            UnityEngine.Debug.LogWarning("[PartSelectView] enemyVisual が見つからない: ヒットボックスを生成できません。");
            return;
        }

        Vector2 spriteSize = new Vector2(1f, 1f);
        int sortingLayerID = 0;
        int baseSortingOrder = 0;
        var sr = enemyVisual.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            spriteSize = sr.sprite.bounds.size; // 敵スプライトの local 単位サイズ
            sortingLayerID = sr.sortingLayerID;
            baseSortingOrder = sr.sortingOrder;
        }
        hitboxParent = enemyVisual;

        var unitSquare = GetOrCreateUnitWhiteSquare();
        float borderT = Mathf.Min(spriteSize.x, spriteSize.y) * hitboxBorderRatio;

        for (int i = 0; i < currentParts.Count; i++)
        {
            var p = currentParts[i];
            float w = Mathf.Max(0.0001f, p.hitboxSize.x * spriteSize.x);
            float h = Mathf.Max(0.0001f, p.hitboxSize.y * spriteSize.y);

            // ルート(中心座標+ピボット)。子4つを枠の各辺として配置する。
            var rootGo = new GameObject($"PartHitbox_{p.id}");
            rootGo.transform.SetParent(enemyVisual, false);
            rootGo.transform.localPosition = new Vector3(
                (p.hitboxCenter.x - 0.5f) * spriteSize.x,
                (p.hitboxCenter.y - 0.5f) * spriteSize.y,
                -0.01f);
            rootGo.transform.localRotation = Quaternion.identity;
            rootGo.transform.localScale = Vector3.one;

            // 4つのエッジ SpriteRenderer。中は描かないので、キャラ絵がそのまま見える。
            var edges = new SpriteRenderer[4];
            edges[0] = MakeEdge(rootGo.transform, "EdgeTop",
                new Vector3(0f, h * 0.5f - borderT * 0.5f, 0f),
                new Vector3(w, borderT, 1f),
                unitSquare, sortingLayerID, baseSortingOrder + hitboxSortingOrderOffset + i * 4);
            edges[1] = MakeEdge(rootGo.transform, "EdgeBottom",
                new Vector3(0f, -h * 0.5f + borderT * 0.5f, 0f),
                new Vector3(w, borderT, 1f),
                unitSquare, sortingLayerID, baseSortingOrder + hitboxSortingOrderOffset + i * 4 + 1);
            edges[2] = MakeEdge(rootGo.transform, "EdgeLeft",
                new Vector3(-w * 0.5f + borderT * 0.5f, 0f, 0f),
                new Vector3(borderT, h, 1f),
                unitSquare, sortingLayerID, baseSortingOrder + hitboxSortingOrderOffset + i * 4 + 2);
            edges[3] = MakeEdge(rootGo.transform, "EdgeRight",
                new Vector3(w * 0.5f - borderT * 0.5f, 0f, 0f),
                new Vector3(borderT, h, 1f),
                unitSquare, sortingLayerID, baseSortingOrder + hitboxSortingOrderOffset + i * 4 + 3);

            hitboxes.Add(new HitboxUi { go = rootGo, edges = edges });
        }
    }

    private SpriteRenderer MakeEdge(Transform parent, string name,
        Vector3 localPos, Vector3 localScale, Sprite unitSquare, int sortingLayerID, int sortingOrder)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.transform.localRotation = Quaternion.identity;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = unitSquare;
        sr.color = hitboxColor;
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    private static Sprite _unitWhiteSquare;
    /// <summary>2x2 ピクセル白テクスチャ、PPU=2 → bounds = 1x1 ローカル単位のスプライト。スケールでサイズ調整しやすい。</summary>
    private static Sprite GetOrCreateUnitWhiteSquare()
    {
        if (_unitWhiteSquare != null) return _unitWhiteSquare;
        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(new Color32[]
        {
            new Color32(255,255,255,255), new Color32(255,255,255,255),
            new Color32(255,255,255,255), new Color32(255,255,255,255),
        });
        tex.Apply();
        _unitWhiteSquare = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        _unitWhiteSquare.name = "PartSelectUnitWhite";
        return _unitWhiteSquare;
    }

    private void BuildChips()
    {
        if (currentParts == null) return;
        float chipW = 220f, chipH = 70f, gap = 18f;
        float total = currentParts.Count * chipW + (currentParts.Count - 1) * gap;
        float startX = -total * 0.5f + chipW * 0.5f;
        for (int i = 0; i < currentParts.Count; i++)
        {
            var part = currentParts[i];
            int hp = runtimeState != null ? runtimeState.GetCurrentHp(part.id) : part.maxHp;

            // チップテキスト設定
            chipTexts[i] = $"{part.displayName}  {hp}/{part.maxHp}";
            chipColors[i] = Color.white;
            chipSelected[i] = false;

            var chipGo = new GameObject($"Chip_{part.id}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            chipGo.transform.SetParent(overlayCanvas.transform, false);
            var rt = chipGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(startX + i * (chipW + gap), 130f);
            rt.sizeDelta = new Vector2(chipW, chipH);
            var img = chipGo.GetComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.18f, 0.95f);
            img.raycastTarget = false;
            var ol = chipGo.AddComponent<Outline>();
            ol.effectColor = new Color(0.95f, 0.15f, 0.15f, 1f);
            ol.effectDistance = new Vector2(3f, -3f);

            // テキストはIMGUIで描画するので、Unity UIのTextは作成しない
            chips.Add(new PartChipUi { go = chipGo, bg = img, outline = ol });
        }
    }

    private void BuildTimer()
    {
        // タイマーテキストはIMGUIで描画

        progressBarWidth = 1600f;
        var pbBg = new GameObject("ProgressBg",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pbBg.transform.SetParent(overlayCanvas.transform, false);
        var pbBgRt = pbBg.GetComponent<RectTransform>();
        pbBgRt.anchorMin = new Vector2(0.5f, 0f);
        pbBgRt.anchorMax = new Vector2(0.5f, 0f);
        pbBgRt.pivot = new Vector2(0.5f, 0f);
        pbBgRt.anchoredPosition = new Vector2(0f, 80f);
        pbBgRt.sizeDelta = new Vector2(progressBarWidth, 24f);
        var bgImg = pbBg.GetComponent<Image>();
        bgImg.color = new Color(0.1f, 0.12f, 0.16f, 1f);
        bgImg.raycastTarget = false;

        var fill = new GameObject("ProgressFill",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(pbBg.transform, false);
        progressFill = fill.GetComponent<RectTransform>();
        progressFill.anchorMin = new Vector2(0f, 0f);
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.anchoredPosition = new Vector2(0f, 0f);
        progressFill.sizeDelta = new Vector2(progressBarWidth, 0f);
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = progressBarColor;
        fillImg.raycastTarget = false;
    }

    private void ApplyHighlight()
    {
        for (int i = 0; i < hitboxes.Count; i++)
        {
            bool sel = (i == CursorIndex);
            var hb = hitboxes[i];
            if (hb.edges != null)
            {
                Color c = sel ? hitboxSelectedColor : hitboxColor;
                for (int e = 0; e < hb.edges.Length; e++)
                    if (hb.edges[e] != null) hb.edges[e].color = c;
            }
        }
        for (int i = 0; i < chips.Count; i++)
        {
            bool sel = (i == CursorIndex);
            if (chips[i].bg != null)
                chips[i].bg.color = sel
                    ? new Color(hitboxSelectedColor.r * 0.7f, hitboxSelectedColor.g * 0.7f, hitboxSelectedColor.b * 0.3f, 1f)
                    : new Color(0.12f, 0.13f, 0.18f, 0.95f);
            if (chips[i].outline != null)
                chips[i].outline.effectColor = sel ? hitboxSelectedColor : new Color(0.95f, 0.15f, 0.15f, 1f);
            // チップの色・選択状態を更新
            if (chipColors != null && i < chipColors.Length)
                chipColors[i] = sel ? Color.black : Color.white;
            if (chipSelected != null && i < chipSelected.Length)
                chipSelected[i] = sel;
        }
        if (guiHelper != null) guiHelper.MarkDirty();
    }

    private IEnumerator PulseRoutine()
    {
        // 選択中ヒットボックスのスケールを軽く脈動。元 localScale を覚えて倍率で乗せる。
        float t = 0f;
        Vector3[] baseScales = new Vector3[hitboxes.Count];
        for (int i = 0; i < hitboxes.Count; i++)
        {
            if (hitboxes[i].go != null) baseScales[i] = hitboxes[i].go.transform.localScale;
        }
        while (true)
        {
            t += Time.deltaTime * 4f;
            float pulse = 1f + Mathf.Sin(t) * 0.08f;
            for (int i = 0; i < hitboxes.Count; i++)
            {
                if (hitboxes[i].go == null) continue;
                Vector3 baseS = (i < baseScales.Length) ? baseScales[i] : Vector3.one;
                hitboxes[i].go.transform.localScale = (i == CursorIndex)
                    ? new Vector3(baseS.x * pulse, baseS.y * pulse, 1f)
                    : baseS;
            }
            yield return null;
        }
    }

    private struct HitboxUi
    {
        public GameObject go;        // 枠ルート(中心+ピボット)
        public SpriteRenderer[] edges; // 上下左右4辺(色変更/フェード用)
    }
    private struct PartChipUi
    {
        public GameObject go;
        public Image bg;
        public Outline outline;
    }

    // チップの位置情報を取得
    public List<Vector2> GetChipPositions()
    {
        var positions = new List<Vector2>();
        float chipW = 220f, chipH = 70f, gap = 18f;
        int count = chipTexts != null ? chipTexts.Length : 0;
        float total = count * chipW + (count - 1) * gap;
        float startX = -total * 0.5f + chipW * 0.5f;
        for (int i = 0; i < count; i++)
        {
            // 画面中央からの相対位置
            float x = Screen.width / 2f + startX + i * (chipW + gap);
            float y = Screen.height - 130f - chipH / 2f; // 下からの位置をスクリーン座標に変換
            positions.Add(new Vector2(x, y));
        }
        return positions;
    }
}

/// <summary>PartSelectView用のIMGUIヘルパー</summary>
public class PartSelectIMGUIHelper : MonoBehaviour
{
    private PartSelectView view;
    private int pixelScale;
    private bool isDirty = true;

    private Texture2D headerTexture;
    private Texture2D timerTexture;
    private Texture2D hintTexture;
    private Texture2D[] chipTextures;

    private string cachedHeader;
    private Color cachedHeaderColor;
    private string cachedTimer;
    private string[] cachedChips;

    public void Initialize(PartSelectView v)
    {
        view = v;
        pixelScale = Mathf.Max(3, Screen.height / 240);

        // ヒントテキストは固定
        hintTexture = PixelFontRenderer.RenderText("   Select / Enter", new Color(0.7f, 0.7f, 0.8f, 1f), pixelScale);
    }

    public void MarkDirty()
    {
        isDirty = true;
    }

    void OnDestroy()
    {
        if (headerTexture != null) Destroy(headerTexture);
        if (timerTexture != null) Destroy(timerTexture);
        if (hintTexture != null) Destroy(hintTexture);
        if (chipTextures != null)
        {
            foreach (var tex in chipTextures)
                if (tex != null) Destroy(tex);
        }
    }

    void OnGUI()
    {
        if (view == null) return;

        float centerX = Screen.width / 2f;

        // ヘッダー描画
        string headerText = view.GetHeaderText();
        Color headerColor = view.GetHeaderColor();
        if (isDirty || headerText != cachedHeader || headerColor != cachedHeaderColor || headerTexture == null)
        {
            if (headerTexture != null) Destroy(headerTexture);
            headerTexture = PixelFontRenderer.RenderText(headerText, headerColor, pixelScale + 2);
            cachedHeader = headerText;
            cachedHeaderColor = headerColor;
        }
        if (headerTexture != null)
        {
            float x = centerX - headerTexture.width / 2f;
            float y = 40f;
            // 影
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x + 2, y + 2, headerTexture.width, headerTexture.height), headerTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, headerTexture.width, headerTexture.height), headerTexture);
        }

        // タイマー描画
        string timerText = view.GetTimerText();
        if (!string.IsNullOrEmpty(timerText))
        {
            if (isDirty || timerText != cachedTimer || timerTexture == null)
            {
                if (timerTexture != null) Destroy(timerTexture);
                timerTexture = PixelFontRenderer.RenderText(timerText, new Color(1f, 0.9f, 0.3f, 1f), pixelScale + 1);
                cachedTimer = timerText;
            }
            if (timerTexture != null)
            {
                float x = Screen.width - 60f - timerTexture.width;
                float y = Screen.height - 230f - timerTexture.height;
                GUI.DrawTexture(new Rect(x, y, timerTexture.width, timerTexture.height), timerTexture);
            }
        }

        // チップテキスト描画
        string[] chipTexts = view.GetChipTexts();
        Color[] chipColors = view.GetChipColors();
        var positions = view.GetChipPositions();

        if (chipTexts != null && chipTexts.Length > 0)
        {
            // テクスチャ配列初期化
            if (chipTextures == null || chipTextures.Length != chipTexts.Length)
            {
                if (chipTextures != null)
                {
                    foreach (var tex in chipTextures)
                        if (tex != null) Destroy(tex);
                }
                chipTextures = new Texture2D[chipTexts.Length];
                cachedChips = new string[chipTexts.Length];
            }

            for (int i = 0; i < chipTexts.Length; i++)
            {
                string cacheKey = $"{chipTexts[i]}_{chipColors[i].r}_{chipColors[i].g}_{chipColors[i].b}";
                if (isDirty || cachedChips[i] != cacheKey || chipTextures[i] == null)
                {
                    if (chipTextures[i] != null) Destroy(chipTextures[i]);
                    chipTextures[i] = PixelFontRenderer.RenderText(chipTexts[i], chipColors[i], pixelScale);
                    cachedChips[i] = cacheKey;
                }

                if (chipTextures[i] != null && i < positions.Count)
                {
                    float x = positions[i].x - chipTextures[i].width / 2f;
                    float y = positions[i].y - chipTextures[i].height / 2f;
                    GUI.DrawTexture(new Rect(x, y, chipTextures[i].width, chipTextures[i].height), chipTextures[i]);
                }
            }
        }

        // ヒント描画
        if (hintTexture != null)
        {
            float x = centerX - hintTexture.width / 2f;
            float y = Screen.height - 24f - hintTexture.height;
            GUI.DrawTexture(new Rect(x, y, hintTexture.width, hintTexture.height), hintTexture);
        }

        isDirty = false;
    }
}
