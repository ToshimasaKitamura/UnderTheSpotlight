using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マップ画面 UI(おしゃれ版・黄+黒テーマ)。
/// 背景に深黒+放射状のゴールド光、スター形ノード(BurstGraphic)、明朝体のヘッダ、装飾 HUD フレーム。
/// 入力: ←→ / WASD / ↑↓ で選択、Enter で突入。
/// </summary>
public class WorldMapView : MonoBehaviour
{
    [Header("テーマ色(黄+黒)")]
    public Color colorBgDeep = new Color(0.04f, 0.035f, 0.03f, 1f);      // ベース黒(少しだけ暖色寄り)
    public Color colorBgGlow = new Color(0.35f, 0.25f, 0.08f, 1f);       // 背景の放射ゴールド
    public Color colorGoldDeep = new Color(0.65f, 0.48f, 0.12f, 1f);     // 深いゴールド(枠)
    public Color colorGoldBright = new Color(1f, 0.85f, 0.25f, 1f);      // 明るいゴールド(ノード基本)
    public Color colorGoldHot = new Color(1f, 0.95f, 0.45f, 1f);         // ハイライト(選択中)
    public Color colorGoldMuted = new Color(0.45f, 0.35f, 0.12f, 1f);    // クリア済み(沈んだゴールド)
    public Color colorCream = new Color(1f, 0.97f, 0.85f, 1f);           // 文字
    public Color colorTextDim = new Color(0.78f, 0.68f, 0.38f, 0.85f);   // サブ文字

    [Header("レイアウト")]
    public Vector2 nodeSize = new Vector2(180f, 180f);
    public float labelGap = 22f;

    [Header("スクロール(縦横ワイドマップ)")]
    [Tooltip("マップ全体の幅(canvas 参照単位)。canvas 幅(1920)より大きいと横スクロールが発生する。")]
    public float mapWidth = 4800f;
    [Tooltip("マップ全体の高さ(canvas 参照単位)。canvas 高(1080)より大きいと縦スクロールが発生する。")]
    public float mapHeight = 1500f;
    [Tooltip("選択ノードを中央に寄せるまでの追従速度。大きいほどキビキビ。")]
    public float scrollLerpSpeed = 6f;
    [Tooltip("canvas 参照幅(CanvasScaler.referenceResolution.x と合わせる)。")]
    public float canvasReferenceWidth = 1920f;
    [Tooltip("canvas 参照高(CanvasScaler.referenceResolution.y と合わせる)。")]
    public float canvasReferenceHeight = 1080f;

    public event Action<StageData> StageSelected;
    public int CursorIndex { get; private set; } = 0;
    public bool IsLocked { get; private set; } = false;

    private Canvas canvas;
    private RectTransform panContainer;
    private float targetScrollX;
    private float targetScrollY;
    private List<NodeUi> nodes = new List<NodeUi>();
    private Text headerLabel;
    private Text expLabel;
    private Text bombLabel;
    private RectTransform bgGlowRt;
    private RectTransform bgGlowCoreRt;
    private WorldMapData worldMap;
    private RunProgress runProgress;
    private Coroutine idleRoutine;
    private Coroutine sparkleRoutine;

    // フォントは GameFont 経由で取得(GameTuning.displayFont / bodyFont で上書き可能)
    private static Font Mincho() => GameFont.Display();
    private static Font Gothic() => GameFont.Body();

    public void Build(WorldMapData map, RunProgress progress)
    {
        Clear();
        worldMap = map;
        runProgress = progress;
        BuildCanvas();
        BuildBackground();
        BuildPanContainer();
        BuildPaths();
        BuildNodes();
        BuildHeader();
        BuildHud();
        BuildControlHint();
        BuildCornerOrnaments();
        CursorIndex = 0;
        ApplyHighlight();
        // 初期スクロールは選択中ノード位置に即時スナップ(縦横とも)
        if (panContainer != null)
            panContainer.anchoredPosition = new Vector2(targetScrollX, targetScrollY);
        idleRoutine = StartCoroutine(IdleAnim());
        sparkleRoutine = StartCoroutine(SparkleRoutine());
    }

    public void Clear()
    {
        if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
        if (sparkleRoutine != null) { StopCoroutine(sparkleRoutine); sparkleRoutine = null; }
        if (canvas != null) { Destroy(canvas.gameObject); canvas = null; }
        nodes.Clear();
        headerLabel = null;
        expLabel = null;
        bombLabel = null;
        worldMap = null;
        runProgress = null;
        CursorIndex = 0;
        IsLocked = false;
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
        // 選択ノードを爆ぜさせる: 拡大しつつ複数のスパークル放出
        if (CursorIndex >= 0 && CursorIndex < nodes.Count)
        {
            var n = nodes[CursorIndex];
            for (int k = 0; k < 12; k++) SpawnSparkle();
            float t = 0f;
            float scaleStart = n.root != null ? n.root.transform.localScale.x : 1.15f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(scaleStart, 1.45f, t / 0.25f);
                if (n.root != null) n.root.transform.localScale = Vector3.one * s;
                yield return null;
            }
        }
        // 画面全体の金フラッシュ(短く)
        var flash = new GameObject("ConfirmFlash",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flash.transform.SetParent(canvas.transform, false);
        var frt = flash.GetComponent<RectTransform>();
        StretchToFill(frt);
        var fimg = flash.GetComponent<Image>();
        fimg.color = new Color(colorGoldHot.r, colorGoldHot.g, colorGoldHot.b, 0f);
        fimg.raycastTarget = false;
        float ft = 0f;
        while (ft < 0.35f)
        {
            ft += Time.deltaTime;
            float p = ft / 0.35f;
            // 0 → 0.55 → 0 でフェードイン・アウト
            float a = p < 0.4f ? Mathf.Lerp(0f, 0.55f, p / 0.4f) : Mathf.Lerp(0.55f, 0f, (p - 0.4f) / 0.6f);
            var c = fimg.color; c.a = a; fimg.color = c;
            yield return null;
        }
        if (flash != null) Destroy(flash);

        StageSelected?.Invoke(stage);
    }

    // ===== Canvas =====
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

    // ===== 背景: 深黒 + 放射ゴールド光 =====
    private void BuildBackground()
    {
        // ベース全画面: 深い黒
        var bgGo = new GameObject("BgBase",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvas.transform, false);
        StretchToFill(bgGo.GetComponent<RectTransform>());
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = colorBgDeep;
        bgImg.raycastTarget = false;

        // worldMap.mapBackground が指定されてれば差し替え(その上にゴールド層をかぶせる)
        if (worldMap != null && worldMap.mapBackground != null)
        {
            bgImg.sprite = worldMap.mapBackground;
            bgImg.color = new Color(0.25f, 0.22f, 0.16f, 1f); // sprite を少しトーンダウン
        }

        // 放射状ゴールド層(画面中央に大きな星バースト、ゆっくり回転)
        var glowGo = new GameObject("BgGlow",
            typeof(RectTransform), typeof(CanvasRenderer));
        glowGo.transform.SetParent(canvas.transform, false);
        bgGlowRt = glowGo.GetComponent<RectTransform>();
        bgGlowRt.anchorMin = new Vector2(0.5f, 0.5f);
        bgGlowRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgGlowRt.pivot = new Vector2(0.5f, 0.5f);
        bgGlowRt.anchoredPosition = Vector2.zero;
        bgGlowRt.sizeDelta = new Vector2(2200f, 2200f);
        var glow = glowGo.AddComponent<BurstGraphic>();
        glow.color = new Color(colorBgGlow.r, colorBgGlow.g, colorBgGlow.b, 0.42f);
        glow.spikes = 18;
        glow.innerRadiusRatio = 0.55f;
        glow.raycastTarget = false;

        // 内側の小さい光(コア、逆方向にゆっくり回転)
        var coreGo = new GameObject("BgGlowCore",
            typeof(RectTransform), typeof(CanvasRenderer));
        coreGo.transform.SetParent(canvas.transform, false);
        bgGlowCoreRt = coreGo.GetComponent<RectTransform>();
        bgGlowCoreRt.anchorMin = new Vector2(0.5f, 0.5f);
        bgGlowCoreRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgGlowCoreRt.pivot = new Vector2(0.5f, 0.5f);
        bgGlowCoreRt.anchoredPosition = Vector2.zero;
        bgGlowCoreRt.sizeDelta = new Vector2(900f, 900f);
        var core = coreGo.AddComponent<BurstGraphic>();
        core.color = new Color(colorGoldDeep.r, colorGoldDeep.g, colorGoldDeep.b, 0.38f);
        core.spikes = 24;
        core.innerRadiusRatio = 0.82f;
        core.raycastTarget = false;

        // 背景の散らばる星(中央のコンテンツバンドを避けてリング状に分布)
        int starCount = 14;
        var rng = new System.Random(12345);
        for (int s = 0; s < starCount; s++)
        {
            // 円形分布: 中央(0.5,0.5)を避ける(0.18..0.5 のリング、ヘッダ/HUD 帯外)
            float r = 0.28f + (float)rng.NextDouble() * 0.32f;
            float theta = (float)rng.NextDouble() * Mathf.PI * 2f;
            float px = 0.5f + Mathf.Cos(theta) * r * 0.82f;
            float py = 0.5f + Mathf.Sin(theta) * r * 0.55f;
            // ヘッダ帯(上 17%)と HUD帯(下 12%、左 25%) を避ける
            if (py > 0.83f) continue;
            if (py < 0.12f && px < 0.30f) continue;

            var sg = new GameObject($"BgStar_{s}",
                typeof(RectTransform), typeof(CanvasRenderer));
            sg.transform.SetParent(canvas.transform, false);
            var srt = sg.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(px, py);
            srt.anchorMax = new Vector2(px, py);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            float size = 4f + (float)rng.NextDouble() * 9f;
            srt.sizeDelta = new Vector2(size, size);
            var b = sg.AddComponent<BurstGraphic>();
            float a = 0.14f + (float)rng.NextDouble() * 0.42f;
            b.color = new Color(colorGoldBright.r, colorGoldBright.g, colorGoldBright.b, a);
            b.spikes = 4;
            b.innerRadiusRatio = 0.25f;
            b.raycastTarget = false;
            sg.transform.localRotation = Quaternion.Euler(0f, 0f, 45f * (float)rng.NextDouble());
        }
    }

    // ===== 縦横スクロール用のパンコンテナ =====
    // すべての「マップ上のもの」(ノード/パス/選択カーソル/スパークル)はここの子に置く。
    // anchoredPosition を XY 両方動かして、選択ノードが canvas 中央に来るよう滑らせる。
    private void BuildPanContainer()
    {
        var go = new GameObject("PanContainer", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        panContainer = go.GetComponent<RectTransform>();
        // canvas 中央を基準にした固定サイズ (mapWidth × mapHeight) 矩形
        panContainer.anchorMin = new Vector2(0.5f, 0.5f);
        panContainer.anchorMax = new Vector2(0.5f, 0.5f);
        panContainer.pivot = new Vector2(0.5f, 0.5f);
        panContainer.anchoredPosition = Vector2.zero;
        panContainer.sizeDelta = new Vector2(mapWidth, mapHeight);
    }

    // ===== ノード間のパス(緩いカーブ + サイズ変化のドット) =====
    private void BuildPaths()
    {
        if (worldMap == null || worldMap.stages == null) return;
        for (int i = 0; i < worldMap.stages.Count - 1; i++)
        {
            var a = worldMap.stages[i];
            var b = worldMap.stages[i + 1];
            if (a == null || b == null) continue;

            Vector2 from = a.nodePosition;
            Vector2 to = b.nodePosition;
            // 二次ベジエ用の中間制御点(垂直方向に少し膨らませる)
            Vector2 mid = (from + to) * 0.5f;
            Vector2 dir = (to - from);
            Vector2 perp = new Vector2(-dir.y, dir.x).normalized * 0.08f;
            Vector2 ctrl = mid + perp;

            int dotCount = 16;
            for (int d = 0; d < dotCount; d++)
            {
                float t = (d + 0.5f) / dotCount;
                Vector2 p1 = Vector2.Lerp(from, ctrl, t);
                Vector2 p2 = Vector2.Lerp(ctrl, to, t);
                Vector2 pos = Vector2.Lerp(p1, p2, t);

                var dotGo = new GameObject($"PathDot_{i}_{d}",
                    typeof(RectTransform), typeof(CanvasRenderer));
                dotGo.transform.SetParent(panContainer.transform, false);
                var rt = dotGo.GetComponent<RectTransform>();
                rt.anchorMin = pos;
                rt.anchorMax = pos;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                // 中央が大きくなるサインカーブで装飾感を出す
                float sizeBase = 8f + Mathf.Sin(t * Mathf.PI) * 8f;
                rt.sizeDelta = new Vector2(sizeBase, sizeBase);
                var burst = dotGo.AddComponent<BurstGraphic>();
                float alpha = 0.55f + Mathf.Sin(t * Mathf.PI) * 0.35f;
                burst.color = new Color(colorGoldDeep.r, colorGoldDeep.g, colorGoldDeep.b, alpha);
                burst.spikes = 14;
                burst.innerRadiusRatio = 0.95f;
                burst.raycastTarget = false;
            }
        }
    }

    // ===== スター形ノード =====
    private void BuildNodes()
    {
        if (worldMap == null) return;
        for (int i = 0; i < worldMap.stages.Count; i++)
        {
            var entry = worldMap.stages[i];
            if (entry == null || entry.stage == null) continue;

            // ノードのルート(配置 + スケール animations 用) — PanContainer 内に置く
            var rootGo = new GameObject($"Node_{entry.stage.id}",
                typeof(RectTransform));
            rootGo.transform.SetParent(panContainer.transform, false);
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = entry.nodePosition;
            rootRt.anchorMax = entry.nodePosition;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = nodeSize;

            // 外側の太い枠(深いゴールド)
            var outerGo = new GameObject("Outer",
                typeof(RectTransform), typeof(CanvasRenderer));
            outerGo.transform.SetParent(rootGo.transform, false);
            var orRt = outerGo.GetComponent<RectTransform>();
            orRt.anchorMin = Vector2.zero; orRt.anchorMax = Vector2.one;
            orRt.offsetMin = Vector2.zero; orRt.offsetMax = Vector2.zero;
            var outer = outerGo.AddComponent<BurstGraphic>();
            outer.color = colorGoldDeep;
            outer.spikes = 10;
            outer.innerRadiusRatio = 0.45f;
            outer.raycastTarget = false;

            // 中身の黒(少し小さく)
            var innerGo = new GameObject("Inner",
                typeof(RectTransform), typeof(CanvasRenderer));
            innerGo.transform.SetParent(rootGo.transform, false);
            var inRt = innerGo.GetComponent<RectTransform>();
            inRt.anchorMin = new Vector2(0.07f, 0.07f);
            inRt.anchorMax = new Vector2(0.93f, 0.93f);
            inRt.offsetMin = Vector2.zero; inRt.offsetMax = Vector2.zero;
            var inner = innerGo.AddComponent<BurstGraphic>();
            inner.color = new Color(0.06f, 0.05f, 0.03f, 1f);
            inner.spikes = 10;
            inner.innerRadiusRatio = 0.45f;
            inner.raycastTarget = false;

            // 中心のゴールドコア
            var coreGo = new GameObject("Core",
                typeof(RectTransform), typeof(CanvasRenderer));
            coreGo.transform.SetParent(rootGo.transform, false);
            var coreRt = coreGo.GetComponent<RectTransform>();
            coreRt.anchorMin = new Vector2(0.32f, 0.32f);
            coreRt.anchorMax = new Vector2(0.68f, 0.68f);
            coreRt.offsetMin = Vector2.zero; coreRt.offsetMax = Vector2.zero;
            var core = coreGo.AddComponent<BurstGraphic>();
            core.color = colorGoldBright;
            core.spikes = 8;
            core.innerRadiusRatio = 0.5f;
            core.raycastTarget = false;

            // ステージ番号(中央、明朝体大)
            var numGo = new GameObject("Num",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            numGo.transform.SetParent(rootGo.transform, false);
            var numRt = numGo.GetComponent<RectTransform>();
            numRt.anchorMin = Vector2.zero; numRt.anchorMax = Vector2.one;
            numRt.offsetMin = Vector2.zero; numRt.offsetMax = Vector2.zero;
            var num = numGo.GetComponent<Text>();
            num.font = Mincho();
            num.fontSize = 64;
            num.fontStyle = FontStyle.Bold;
            num.alignment = TextAnchor.MiddleCenter;
            num.color = new Color(0.06f, 0.05f, 0.03f, 1f); // 黒文字 on gold core
            num.text = (i + 1).ToString();
            var nol = numGo.GetComponent<Outline>();
            nol.effectColor = new Color(colorGoldHot.r, colorGoldHot.g, colorGoldHot.b, 0.5f);
            nol.effectDistance = new Vector2(1f, -1f);

            // ステージ名(ノード下にエレガントなラベル)
            var lblGo = new GameObject("Name",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            lblGo.transform.SetParent(rootGo.transform, false);
            var lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0.5f, 0f);
            lblRt.anchorMax = new Vector2(0.5f, 0f);
            lblRt.pivot = new Vector2(0.5f, 1f);
            lblRt.anchoredPosition = new Vector2(0f, -labelGap);
            lblRt.sizeDelta = new Vector2(440f, 64f);
            var lbl = lblGo.GetComponent<Text>();
            lbl.font = Mincho();
            lbl.fontSize = 30;
            lbl.alignment = TextAnchor.UpperCenter;
            lbl.color = colorCream;
            lbl.text = entry.stage.displayName;
            lbl.fontStyle = FontStyle.Bold;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            var lblOl = lblGo.GetComponent<Outline>();
            lblOl.effectColor = new Color(0f, 0f, 0f, 0.95f);
            lblOl.effectDistance = new Vector2(2f, -2f);

            bool cleared = runProgress != null && runProgress.clearedStageIds.Contains(entry.stage.id);

            // クリア印: ノードの「外」右上に大きな星★(別 GameObject、外側装飾)
            if (cleared)
            {
                var markRoot = new GameObject("ClearedMark", typeof(RectTransform));
                markRoot.transform.SetParent(rootGo.transform, false);
                var mrt = markRoot.GetComponent<RectTransform>();
                mrt.anchorMin = new Vector2(1f, 1f);
                mrt.anchorMax = new Vector2(1f, 1f);
                mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.anchoredPosition = new Vector2(-6f, -6f); // ノード右上外側
                mrt.sizeDelta = new Vector2(58f, 58f);

                // 黒い外枠星(コントラスト)
                var bg = new GameObject("MarkBg",
                    typeof(RectTransform), typeof(CanvasRenderer));
                bg.transform.SetParent(markRoot.transform, false);
                var bgrt = bg.GetComponent<RectTransform>();
                bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
                bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
                var bgb = bg.AddComponent<BurstGraphic>();
                bgb.color = new Color(0.05f, 0.04f, 0.02f, 1f);
                bgb.spikes = 5;
                bgb.innerRadiusRatio = 0.4f;
                bgb.raycastTarget = false;

                // ゴールド本体
                var star = new GameObject("MarkStar",
                    typeof(RectTransform), typeof(CanvasRenderer));
                star.transform.SetParent(markRoot.transform, false);
                var srt = star.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.12f, 0.12f);
                srt.anchorMax = new Vector2(0.88f, 0.88f);
                srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
                var sb = star.AddComponent<BurstGraphic>();
                sb.color = colorGoldHot;
                sb.spikes = 5;
                sb.innerRadiusRatio = 0.42f;
                sb.raycastTarget = false;
            }

            nodes.Add(new NodeUi
            {
                root = rootGo,
                outer = outer,
                inner = inner,
                core = core,
                num = num,
                label = lbl,
                cleared = cleared,
                anchor = entry.nodePosition,
                stage = entry.stage,
            });
        }
    }

    // ===== ヘッダ(明朝体・大きく装飾) =====
    private void BuildHeader()
    {
        // 帯状の黒い背景バー(横にうっすらゴールドのライン)
        var barGo = new GameObject("HeaderBar",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barGo.transform.SetParent(canvas.transform, false);
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, 0f);
        barRt.sizeDelta = new Vector2(0f, 170f);
        var barImg = barGo.GetComponent<Image>();
        barImg.color = new Color(0f, 0f, 0f, 0.82f);
        barImg.raycastTarget = false;

        // 帯の下のゴールド線
        var lineGo = new GameObject("HeaderLine",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineGo.transform.SetParent(barGo.transform, false);
        var lineRt = lineGo.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0f);
        lineRt.anchorMax = new Vector2(1f, 0f);
        lineRt.pivot = new Vector2(0.5f, 0f);
        lineRt.anchoredPosition = Vector2.zero;
        lineRt.sizeDelta = new Vector2(0f, 4f);
        var lineImg = lineGo.GetComponent<Image>();
        lineImg.color = colorGoldDeep;
        lineImg.raycastTarget = false;

        // ヘッダ装飾: 中央タイトルから外側へ伸びる細い線 + 菱形(両側 2 つずつ)
        // 左右対称に [菱形・線・菱形・線] のパターン
        for (int side = -1; side <= 1; side += 2)
        {
            // 内側の小ダイヤ(タイトル直近)
            CreateDiamond(barGo.transform, new Vector2(side * 360f, -10f), 22f, colorGoldHot);
            // 中間の細線
            CreateLine(barGo.transform, new Vector2(side * 430f, -10f), new Vector2(80f, 2f), colorGoldBright);
            // 外側の小ダイヤ
            CreateDiamond(barGo.transform, new Vector2(side * 500f, -10f), 14f, colorGoldDeep);
            // さらに外の長い線
            CreateLine(barGo.transform, new Vector2(side * 580f, -10f), new Vector2(120f, 1.5f), colorGoldDeep);
        }

        // タイトル本体(明朝体・太字)
        var hGo = new GameObject("HeaderTitle",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        hGo.transform.SetParent(barGo.transform, false);
        var hRt = hGo.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.5f, 0.5f);
        hRt.anchorMax = new Vector2(0.5f, 0.5f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        hRt.anchoredPosition = new Vector2(0f, -10f);
        hRt.sizeDelta = new Vector2(1200f, 120f);
        headerLabel = hGo.GetComponent<Text>();
        headerLabel.font = Mincho();
        headerLabel.fontSize = 76;
        headerLabel.alignment = TextAnchor.MiddleCenter;
        headerLabel.color = colorGoldHot;
        headerLabel.fontStyle = FontStyle.Bold;
        headerLabel.text = SpaceJapanese(worldMap != null ? worldMap.displayName : "ワールドマップ");
        headerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        headerLabel.verticalOverflow = VerticalWrapMode.Overflow;
        var hOl = hGo.GetComponent<Outline>();
        hOl.effectColor = new Color(0f, 0f, 0f, 0.95f);
        hOl.effectDistance = new Vector2(3f, -3f);

        // 小さな英数サブタイトル
        var subGo = new GameObject("HeaderSub",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        subGo.transform.SetParent(barGo.transform, false);
        var srt = subGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0f);
        srt.anchorMax = new Vector2(0.5f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0f, 10f);
        srt.sizeDelta = new Vector2(900f, 40f);
        var sub = subGo.GetComponent<Text>();
        sub.font = Mincho();
        sub.fontSize = 24;
        sub.alignment = TextAnchor.MiddleCenter;
        sub.color = colorGoldDeep;
        sub.text = "S T A G E    S E L E C T";
        var sol = subGo.GetComponent<Outline>();
        sol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sol.effectDistance = new Vector2(1f, -1f);
    }

    // ===== 左下 HUD (EXP/BOMB) =====
    private void BuildHud()
    {
        // 枠
        var frameGo = new GameObject("HudFrame",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        frameGo.transform.SetParent(canvas.transform, false);
        var frt = frameGo.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(0f, 0f);
        frt.pivot = new Vector2(0f, 0f);
        frt.anchoredPosition = new Vector2(40f, 40f);
        frt.sizeDelta = new Vector2(380f, 130f);
        var fimg = frameGo.GetComponent<Image>();
        fimg.color = new Color(0f, 0f, 0f, 0.78f);
        fimg.raycastTarget = false;
        var ol = frameGo.AddComponent<Outline>();
        ol.effectColor = colorGoldDeep;
        ol.effectDistance = new Vector2(3f, -3f);

        // 上の細いゴールドストライプ
        var stripeGo = new GameObject("HudStripe",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        stripeGo.transform.SetParent(frameGo.transform, false);
        var strt = stripeGo.GetComponent<RectTransform>();
        strt.anchorMin = new Vector2(0f, 1f);
        strt.anchorMax = new Vector2(1f, 1f);
        strt.pivot = new Vector2(0.5f, 1f);
        strt.anchoredPosition = Vector2.zero;
        strt.sizeDelta = new Vector2(0f, 6f);
        stripeGo.GetComponent<Image>().color = colorGoldBright;

        // HUD 四隅にダイヤ装飾
        for (int hc = 0; hc < 4; hc++)
        {
            float ax = (hc % 2 == 0) ? 0f : 1f;
            float ay = (hc < 2) ? 0f : 1f;
            float dx = (hc % 2 == 0) ? 10f : -10f;
            float dy = (hc < 2) ? 10f : -10f;
            var corner = new GameObject($"HudCorner_{hc}",
                typeof(RectTransform), typeof(CanvasRenderer));
            corner.transform.SetParent(frameGo.transform, false);
            var crt = corner.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(ax, ay);
            crt.anchorMax = new Vector2(ax, ay);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(dx, dy);
            crt.sizeDelta = new Vector2(10f, 10f);
            var cb = corner.AddComponent<BurstGraphic>();
            cb.color = colorGoldBright;
            cb.spikes = 4;
            cb.innerRadiusRatio = 0.3f;
            cb.raycastTarget = false;
            corner.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        // EXP アイコン(ダイヤ)
        CreateDiamond(frameGo.transform, new Vector2(40f, 32f), 24f, colorGoldHot);

        // EXP 行(数字を大きく、ラベルは控えめ)
        var expGo = new GameObject("Exp",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        expGo.transform.SetParent(frameGo.transform, false);
        var ert = expGo.GetComponent<RectTransform>();
        ert.anchorMin = new Vector2(0f, 0.5f);
        ert.anchorMax = new Vector2(1f, 1f);
        ert.offsetMin = new Vector2(72f, 0f);
        ert.offsetMax = new Vector2(-20f, -8f);
        expLabel = expGo.GetComponent<Text>();
        expLabel.font = Mincho();
        expLabel.fontSize = 28;
        expLabel.alignment = TextAnchor.MiddleLeft;
        expLabel.color = colorGoldHot;
        expLabel.fontStyle = FontStyle.Bold;
        expLabel.text = $"EXP   {(runProgress != null ? runProgress.accumulatedExp : 0)}";

        // BOMB アイコン(円)
        var bIcon = new GameObject("BombIcon",
            typeof(RectTransform), typeof(CanvasRenderer));
        bIcon.transform.SetParent(frameGo.transform, false);
        var bIconRt = bIcon.GetComponent<RectTransform>();
        bIconRt.anchorMin = new Vector2(0f, 0f);
        bIconRt.anchorMax = new Vector2(0f, 0f);
        bIconRt.pivot = new Vector2(0.5f, 0.5f);
        bIconRt.anchoredPosition = new Vector2(40f, 32f);
        bIconRt.sizeDelta = new Vector2(18f, 18f);
        var bBurst = bIcon.AddComponent<BurstGraphic>();
        bBurst.color = colorGoldDeep;
        bBurst.spikes = 16; // 円に近い
        bBurst.innerRadiusRatio = 0.95f;
        bBurst.raycastTarget = false;

        // BOMB 行
        var bombGo = new GameObject("Bomb",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        bombGo.transform.SetParent(frameGo.transform, false);
        var brt = bombGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0.5f);
        brt.offsetMin = new Vector2(72f, 6f);
        brt.offsetMax = new Vector2(-20f, 0f);
        bombLabel = bombGo.GetComponent<Text>();
        bombLabel.font = Gothic();
        bombLabel.fontSize = 22;
        bombLabel.alignment = TextAnchor.MiddleLeft;
        bombLabel.color = colorTextDim;
        bombLabel.text = $"BOMB  +{(runProgress != null ? runProgress.accumulatedBombs : 0)}";
    }

    // ===== 操作ヒント =====
    private void BuildControlHint()
    {
        var cGo = new GameObject("ControlHint",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        cGo.transform.SetParent(canvas.transform, false);
        var cRt = cGo.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(1f, 0f);
        cRt.anchorMax = new Vector2(1f, 0f);
        cRt.pivot = new Vector2(1f, 0f);
        cRt.anchoredPosition = new Vector2(-40f, 50f);
        cRt.sizeDelta = new Vector2(560f, 50f);
        var ch = cGo.GetComponent<Text>();
        ch.font = Gothic();
        ch.fontSize = 22;
        ch.alignment = TextAnchor.MiddleRight;
        ch.color = colorTextDim;
        ch.text = "←→で選択   /   ENTER で突入";
        var ol = cGo.GetComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
        ol.effectDistance = new Vector2(1f, -1f);
    }

    // ===== 四隅装飾(アール・デコ風) =====
    private void BuildCornerOrnaments()
    {
        for (int corner = 0; corner < 4; corner++)
        {
            float ax = (corner % 2 == 0) ? 0f : 1f;
            float ay = (corner < 2) ? 0f : 1f;
            float dx = (corner % 2 == 0) ? 40f : -40f;
            float dy = (corner < 2) ? 40f : -40f;

            // L字の小さなゴールドのカドマーク(2本の線)
            var v = new GameObject($"OrnV_{corner}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            v.transform.SetParent(canvas.transform, false);
            var vrt = v.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(ax, ay);
            vrt.anchorMax = new Vector2(ax, ay);
            vrt.pivot = new Vector2(ax, ay);
            vrt.anchoredPosition = new Vector2(dx, dy);
            vrt.sizeDelta = new Vector2(4f, 70f);
            v.GetComponent<Image>().color = colorGoldBright;

            var h = new GameObject($"OrnH_{corner}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            h.transform.SetParent(canvas.transform, false);
            var hrt = h.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(ax, ay);
            hrt.anchorMax = new Vector2(ax, ay);
            hrt.pivot = new Vector2(ax, ay);
            hrt.anchoredPosition = new Vector2(dx, dy);
            hrt.sizeDelta = new Vector2(70f, 4f);
            h.GetComponent<Image>().color = colorGoldBright;

            // 小さな菱形
            var d = new GameObject($"OrnDot_{corner}",
                typeof(RectTransform), typeof(CanvasRenderer));
            d.transform.SetParent(canvas.transform, false);
            var drt = d.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(ax, ay);
            drt.anchorMax = new Vector2(ax, ay);
            drt.pivot = new Vector2(ax, ay);
            float ddx = (corner % 2 == 0) ? 76f : -76f;
            float ddy = (corner < 2) ? 76f : -76f;
            drt.anchoredPosition = new Vector2(ddx, ddy);
            drt.sizeDelta = new Vector2(14f, 14f);
            var bd = d.AddComponent<BurstGraphic>();
            bd.color = colorGoldBright;
            bd.spikes = 4;
            bd.innerRadiusRatio = 0.3f;
            bd.raycastTarget = false;
            d.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
    }

    // ===== Highlight =====
    private void ApplyHighlight()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n.root == null) continue;
            bool sel = (i == CursorIndex);
            if (sel)
            {
                if (n.outer != null) n.outer.color = colorGoldHot;
                if (n.core != null) n.core.color = colorGoldHot;
                if (n.num != null) n.num.color = new Color(0.05f, 0.04f, 0.02f, 1f);
                if (n.label != null) n.label.color = colorGoldHot;
                n.root.transform.localScale = Vector3.one * 1.15f;
            }
            else
            {
                if (n.outer != null) n.outer.color = n.cleared ? colorGoldMuted : colorGoldDeep;
                if (n.core != null) n.core.color = n.cleared ? colorGoldMuted : colorGoldBright;
                if (n.num != null) n.num.color = new Color(0.06f, 0.05f, 0.03f, 1f);
                if (n.label != null) n.label.color = colorCream;
                n.root.transform.localScale = Vector3.one;
            }
        }
        UpdateScrollTarget();
    }

    /// <summary>選択中ノードを画面中央に持ってくるスクロール目標値を再計算(縦横とも)。</summary>
    private void UpdateScrollTarget()
    {
        if (panContainer == null || nodes.Count == 0) return;
        if (CursorIndex < 0 || CursorIndex >= nodes.Count) return;
        Vector2 nodeAnchor = nodes[CursorIndex].anchor;
        // container は canvas 中央にアンカーされ pivot (0.5, 0.5)。
        // 内部の anchor(0..1) ノードを canvas 中央(=container アンカー位置)に持ってくるには
        // container を逆方向にオフセットする: (0.5 - anchor) * size
        targetScrollX = (0.5f - nodeAnchor.x) * mapWidth;
        targetScrollY = (0.5f - nodeAnchor.y) * mapHeight;
    }

    // ===== スパークル(選択ノード周囲に小さな星を散らす) =====
    private IEnumerator SparkleRoutine()
    {
        while (true)
        {
            float wait = 0.22f + UnityEngine.Random.value * 0.28f;
            yield return new WaitForSeconds(wait);
            if (CursorIndex >= 0 && CursorIndex < nodes.Count)
                SpawnSparkle();
        }
    }

    private void SpawnSparkle()
    {
        if (canvas == null || nodes.Count == 0) return;
        if (CursorIndex < 0 || CursorIndex >= nodes.Count) return;
        Vector2 nodeAnchor = nodes[CursorIndex].anchor;

        var go = new GameObject("Sparkle",
            typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(panContainer != null ? panContainer.transform : canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = nodeAnchor;
        rt.anchorMax = nodeAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 off = UnityEngine.Random.insideUnitCircle * 110f + new Vector2(0f, 30f);
        rt.anchoredPosition = off;
        float startSize = 8f + UnityEngine.Random.value * 8f;
        rt.sizeDelta = new Vector2(startSize, startSize);
        var b = go.AddComponent<BurstGraphic>();
        b.color = UnityEngine.Random.value < 0.5f ? colorGoldHot : colorCream;
        b.spikes = 4;
        b.innerRadiusRatio = 0.22f;
        b.raycastTarget = false;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 90f);

        StartCoroutine(FadeAndDestroySparkle(go, b, startSize, 0.9f + UnityEngine.Random.value * 0.4f));
    }

    private IEnumerator FadeAndDestroySparkle(GameObject go, BurstGraphic gfx, float startSize, float duration)
    {
        float t = 0f;
        Color baseColor = gfx.color;
        var rt = go.GetComponent<RectTransform>();
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            // 拡大しつつフェード
            float s = startSize * (1f + p * 0.6f);
            if (rt != null) rt.sizeDelta = new Vector2(s, s);
            var c = baseColor;
            c.a = (1f - p) * baseColor.a;
            gfx.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ===== Idle animations: 選択中ノード脈動 + 選択カーソル ホバー =====
    private IEnumerator IdleAnim()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(t * 3f) * 0.06f;
            if (CursorIndex >= 0 && CursorIndex < nodes.Count)
            {
                var sel = nodes[CursorIndex];
                if (sel.root != null) sel.root.transform.localScale = Vector3.one * (1.15f * pulse);
            }
            // 背景の放射光: 互いに逆回転でゆっくり
            if (bgGlowRt != null)
                bgGlowRt.localRotation = Quaternion.Euler(0f, 0f, t * 4f);
            if (bgGlowCoreRt != null)
                bgGlowCoreRt.localRotation = Quaternion.Euler(0f, 0f, -t * 6f);
            // スクロール: 選択ノードを画面中央に寄せる(縦横とも)
            if (panContainer != null)
            {
                Vector2 pos = panContainer.anchoredPosition;
                pos.x = Mathf.Lerp(pos.x, targetScrollX, Time.deltaTime * scrollLerpSpeed);
                pos.y = Mathf.Lerp(pos.y, targetScrollY, Time.deltaTime * scrollLerpSpeed);
                panContainer.anchoredPosition = pos;
            }
            yield return null;
        }
    }

    // ===== ユーティリティ =====
    private static void StretchToFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void CreateDiamond(Transform parent, Vector2 anchoredPos, float size, Color color)
    {
        var d = new GameObject("Diamond",
            typeof(RectTransform), typeof(CanvasRenderer));
        d.transform.SetParent(parent, false);
        var rt = d.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);
        var b = d.AddComponent<BurstGraphic>();
        b.color = color;
        b.spikes = 4;
        b.innerRadiusRatio = 0.3f;
        b.raycastTarget = false;
        d.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void CreateLine(Transform parent, Vector2 anchoredPos, Vector2 size, Color color)
    {
        var line = new GameObject("Line",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.transform.SetParent(parent, false);
        var rt = line.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = line.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    /// <summary>日本語に薄くスペースを入れて高級感を出す。「第一世界」→「第 一 世 界」</summary>
    private static string SpaceJapanese(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append(s[i]);
            if (i < s.Length - 1) sb.Append(' '); // thin space
        }
        return sb.ToString();
    }

    private struct NodeUi
    {
        public GameObject root;
        public BurstGraphic outer;
        public BurstGraphic inner;
        public BurstGraphic core;
        public Text num;
        public Text label;
        public bool cleared;
        public Vector2 anchor;     // 元の StageEntry.nodePosition (null 行スキップ時のインデックスズレ回避)
        public StageData stage;    // 確定でこのノードに紐づく StageData
    }
}

