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
    private Text headerLabel;
    private Text timerLabel;
    private RectTransform progressFill;
    private float progressBarWidth;

    private float timeLimit;
    private float timeLeft;

    private List<EnemyPart> currentParts;
    private List<HitboxUi> hitboxes = new List<HitboxUi>();
    private List<PartChipUi> chips = new List<PartChipUi>();
    private EnemyRuntimeState runtimeState;
    private Coroutine pulseRoutine;

    // フォントは GameFont 経由(GameTuning.bodyFont で上書き可能)
    private static Font UiFont() => GameFont.Body();

    /// <summary>
    /// 構築。<paramref name="enemyVisual"/> は シーン上の Enemy GameObject の Visual 子(SpriteRenderer を持つ Transform)。
    /// この transform の子として world-space ヒットボックスを生成する → 既存の浮遊モーションにも追従。
    /// </summary>
    public void Build(IList<EnemyPart> parts, EnemyRuntimeState state, Transform enemyVisual)
    {
        Clear();
        currentParts = new List<EnemyPart>(parts);
        runtimeState = state;
        BuildOverlayCanvas();
        BuildHitboxesOnEnemy(enemyVisual);
        BuildChips();
        BuildTimer();
        CursorIndex = 0;
        ApplyHighlight();
        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    public void SetHeader(string text, Color tint)
    {
        if (headerLabel == null) return;
        headerLabel.text = text;
        headerLabel.color = tint;
    }

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
        if (timerLabel != null)
            timerLabel.text = timeLimit > 0f ? $"残り {Mathf.Max(0f, timeLeft):F1} 秒" : "";
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
        hitboxParent = null;
        headerLabel = null;
        timerLabel = null;
        progressFill = null;
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

        // ヘッダ
        var headerGo = new GameObject("Header", typeof(RectTransform), typeof(Text));
        headerGo.transform.SetParent(overlayCanvas.transform, false);
        var headerRt = headerGo.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -40f);
        headerRt.sizeDelta = new Vector2(1500f, 80f);
        headerLabel = headerGo.GetComponent<Text>();
        headerLabel.font = UiFont();
        headerLabel.fontSize = 48;
        headerLabel.alignment = TextAnchor.MiddleCenter;
        headerLabel.color = Color.white;
        headerLabel.text = "部位を選んで！";
        headerLabel.fontStyle = FontStyle.Bold;
        headerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        headerLabel.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = headerGo.AddComponent<Outline>();
        ol.effectColor = Color.black;
        ol.effectDistance = new Vector2(2f, -2f);

        // 操作ヒント
        var hintGo = new GameObject("ControlHint", typeof(RectTransform), typeof(Text));
        hintGo.transform.SetParent(overlayCanvas.transform, false);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 24f);
        hintRt.sizeDelta = new Vector2(1400f, 40f);
        var hint = hintGo.GetComponent<Text>();
        hint.font = UiFont();
        hint.fontSize = 24;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.7f, 0.7f, 0.8f, 1f);
        hint.text = "↑↓ or ←→ で選択 / Enter で確定";
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

            var textGo = new GameObject("T", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(chipGo.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var label = textGo.GetComponent<Text>();
            label.font = UiFont();
            label.fontSize = 28;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = $"{part.displayName}  {hp}/{part.maxHp}";

            chips.Add(new PartChipUi { go = chipGo, bg = img, outline = ol, label = label });
        }
    }

    private void BuildTimer()
    {
        var tlGo = new GameObject("TimerLabel", typeof(RectTransform), typeof(Text));
        tlGo.transform.SetParent(overlayCanvas.transform, false);
        var trt = tlGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(1f, 0f);
        trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot = new Vector2(1f, 0f);
        trt.anchoredPosition = new Vector2(-60f, 230f);
        trt.sizeDelta = new Vector2(360f, 60f);
        timerLabel = tlGo.GetComponent<Text>();
        timerLabel.font = UiFont();
        timerLabel.fontSize = 32;
        timerLabel.alignment = TextAnchor.MiddleRight;
        timerLabel.color = new Color(1f, 0.9f, 0.3f, 1f);
        timerLabel.fontStyle = FontStyle.Bold;
        timerLabel.text = "";

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
            if (chips[i].label != null)
                chips[i].label.color = sel ? Color.black : Color.white;
        }
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
        public Text label;
    }
}
