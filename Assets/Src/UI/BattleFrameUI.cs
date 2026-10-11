using System.Collections;
using UnityEngine;

/// <summary>
/// バトル画面のフレームUI。
/// 上半分にボス用フレーム、下半分に4人の味方キャラクター用フレームを配置。
/// </summary>
public class BattleFrameUI : MonoBehaviour
{
    [Header("フレーム設定")]
    [Tooltip("フレームの色")]
    [SerializeField] private Color frameColor = new Color(0.2f, 0.2f, 0.3f, 1f);
    [Tooltip("フレームの枠線の太さ")]
    [SerializeField] private float borderWidth = 0.1f;
    [Tooltip("フレーム間のマージン")]
    [SerializeField] private float margin = 0.2f;

    [Header("四隅ポートレート(窓)")]
    [Tooltip("キャラ幅を窓幅の何倍にするか(1前後。大きすぎると顔が過大に)")]
    [SerializeField] private float portraitWidthFill = 0.65f;
    [Tooltip("頭上の余白(窓内側高さ比)。浮遊で頭が切れない程度に確保")]
    [Range(0f, 0.4f)][SerializeField] private float portraitTopMargin = 0.12f;

    [Header("共有HPゲージ設定")]
    [Tooltip("HPゲージの高さ")]
    [SerializeField] private float hpGaugeHeight = 0.2f;
    [Tooltip("HPゲージとフレームの間隔")]
    [SerializeField] private float hpGaugeMargin = 0.08f;
    [Tooltip("HPゲージの背景色")]
    [SerializeField] private Color hpGaugeBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    [Tooltip("HPゲージの色")]
    [SerializeField] private Color hpGaugeFillColor = new Color(0.2f, 0.8f, 0.3f, 1f);
    [Tooltip("HPゲージの枠線色")]
    [SerializeField] private Color hpGaugeBorderColor = new Color(0.4f, 0.4f, 0.5f, 1f);

    [Header("生成されたフレーム")]
    public Transform bossFrame;
    public Transform[] allyFrames;

    [Header("共有HPゲージ")]
    public Transform sharedHpGauge;
    public Transform sharedHpFill;
    private float sharedHpGaugeWidth;

    [Header("HPゲージパーティクル設定")]
    [Tooltip("パーティクルの色")]
    [SerializeField] private Color particleColor = new Color(0.3f, 1f, 0.5f, 1f);
    [Tooltip("パーティクルの発生数")]
    [SerializeField] private int particleCount = 30;
    [Tooltip("パーティクルの速度")]
    [SerializeField] private float particleSpeed = 2f;
    [Tooltip("パーティクルの寿命")]
    [SerializeField] private float particleLifetime = 0.8f;

    private ParticleSystem hpParticleSystem;

    // フレームの位置情報（他のスクリプトから参照用）
    public Bounds BossFrameBounds { get; private set; }
    public Bounds[] AllyFrameBounds { get; private set; }

    // 窓クリップ用: 各味方枠に窓マスク(SpriteMask)を置き、そこに収まるキャラだけ描く。
    // 隣の窓への映り込みは、ポートレートの高さを「同列の下の窓に届かない」よう制限して防ぐ。
    private readonly SpriteMask[] allyWindowMasks = new SpriteMask[4];
    private readonly SpriteRenderer[][] windowCharSrs = new SpriteRenderer[4][]; // 枠にドックしたキャラの描画群

    // カード選択演出用の位置情報
    private Vector3[] originalAllyPositions;
    private Vector3 collapseTargetPosition;  // 左端の集合位置
    private float allyFrameWidth;
    private float allyFrameHeight;

    private Camera mainCamera;

    // 実際にプレイしている人数(1..4)。ConfigureForPlayerCount で設定。
    // 1人=ソロ(MOTHER2風にボスの下に1枠) / 2人以上=四隅、余り枠はグレーアウト。
    private int activePlayerCount = 4;
    public int ActivePlayerCount => activePlayerCount;
    private static readonly Color DimmedFrameColor = new Color(0.28f, 0.28f, 0.32f, 0.55f);

    private void Awake()
    {
        mainCamera = Camera.main;
        CreateFrames();
    }

    /// <summary>
    /// プレイ人数に応じて味方枠の表示を決める。BossIntro が味方配置の前に呼ぶ。
    /// 人数に関わらず四隅の枠は常に表示し、人数を超えた枠はグレーアウトして「空枠」を示す
    /// (ソロでもボス下に1枠にはしない)。
    /// </summary>
    public void ConfigureForPlayerCount(int count)
    {
        activePlayerCount = Mathf.Clamp(count, 1, 4);
        if (allyFrames == null) return;

        // 人数に関わらず四隅の枠は常に表示する。空席はグレーアウトして「空枠」を示す。
        // (以前はソロ時にボス下へ1枠だけ出す仕様だったが廃止)
        ApplySlotVisibility();
    }

    /// <summary>activePlayerCount に従って各味方枠の表示/グレーアウトを適用する。</summary>
    private void ApplySlotVisibility()
    {
        if (allyFrames == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (allyFrames[i] == null) continue;
            bool used = i < activePlayerCount;
            // 人数に関わらず四隅の全枠を表示。空席はグレーアウトして空枠にする。
            allyFrames[i].gameObject.SetActive(true);
            if (!used) SetFrameColor(allyFrames[i], DimmedFrameColor);
        }
    }

    /// <summary>枠(背景＋枠線)の色を一括設定する(グレーアウト用)。</summary>
    private void SetFrameColor(Transform frame, Color color)
    {
        var srs = frame.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null) srs[i].color = color;
    }

    /// <summary>
    /// フレームを生成
    /// </summary>
    private void CreateFrames()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[BattleFrameUI] メインカメラが見つかりません");
            return;
        }

        // カメラのビューポート境界を取得
        float camHeight = mainCamera.orthographicSize * 2f;
        float camWidth = camHeight * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;

        float left = camPos.x - camWidth / 2f + margin;
        float right = camPos.x + camWidth / 2f - margin;
        float top = camPos.y + camHeight / 2f - margin;
        float bottom = camPos.y - camHeight / 2f + margin;

        // ===== OMORI 風: ボスは画面中央、味方(キャラ入りカード)は四隅 =====

        // ボスフレーム（中央）
        float bossFrameWidth = camWidth * 0.36f;
        float bossFrameHeight = camHeight * 0.50f;
        Vector3 bossFrameCenter = new Vector3(camPos.x, camPos.y, 0f);

        bossFrame = CreateSingleFrame("BossFrame", bossFrameCenter, bossFrameWidth, bossFrameHeight);
        BossFrameBounds = new Bounds(bossFrameCenter, new Vector3(bossFrameWidth, bossFrameHeight, 0f));

        // 味方フレーム（四隅、4つ）。P1=左上 / P2=右上 / P3=左下 / P4=右下。
        allyFrames = new Transform[4];
        AllyFrameBounds = new Bounds[4];
        originalAllyPositions = new Vector3[4];

        allyFrameWidth = camWidth * 0.23f;
        allyFrameHeight = camHeight * 0.33f;

        float axL = left + allyFrameWidth / 2f;    // 左列の中心x
        float axR = right - allyFrameWidth / 2f;   // 右列の中心x
        float ayT = top - allyFrameHeight / 2f;     // 上段の中心y
        float ayB = bottom + allyFrameHeight / 2f;  // 下段の中心y
        Vector3[] corners =
        {
            new Vector3(axL, ayT, 0f), // P1 左上
            new Vector3(axR, ayT, 0f), // P2 右上
            new Vector3(axL, ayB, 0f), // P3 左下
            new Vector3(axR, ayB, 0f), // P4 右下
        };

        // 集合位置(未使用の演出用フォールバック)。画面外左に置く。
        collapseTargetPosition = new Vector3(left - allyFrameWidth, camPos.y, 0f);

        for (int i = 0; i < 4; i++)
        {
            Color pc = (GameColorScheme.Players != null && i < GameColorScheme.Players.Length)
                ? GameColorScheme.Players[i] : frameColor;
            allyFrames[i] = CreateSingleFrame($"AllyFrame_{i + 1}", corners[i], allyFrameWidth, allyFrameHeight, pc);
            AllyFrameBounds[i] = new Bounds(corners[i], new Vector3(allyFrameWidth, allyFrameHeight, 0f));
            originalAllyPositions[i] = corners[i];

            // 窓マスク(枠の内側サイズ)。この窓に収まるキャラだけが見える=下からはみ出さない。
            allyWindowMasks[i] = CreateWindowMask(allyFrames[i],
                allyFrameWidth - borderWidth * 2f, allyFrameHeight - borderWidth * 2f);
        }

        // 共有HPゲージ（ボスフレームのすぐ下。ゲージ・演出ともに位置だけここへ）
        float sharedWidth = camWidth * 0.42f;
        Vector3 sharedHpCenter = new Vector3(
            BossFrameBounds.center.x,
            BossFrameBounds.min.y - hpGaugeMargin - hpGaugeHeight / 2f,
            0f);
        sharedHpGaugeWidth = sharedWidth;
        CreateSharedHpGauge(sharedHpCenter, sharedWidth, hpGaugeHeight);

        // HPゲージパーティクルを作成
        CreateHpParticleSystem(sharedHpCenter, sharedWidth, hpGaugeHeight);

        Debug.Log($"[BattleFrameUI] フレーム生成完了(OMORI風): ボス中央1個、味方四隅4個");
    }

    /// <summary>
    /// 単一のフレームを生成。border に枠線色を渡すとカード風の縁取りになる(未指定は既定 frameColor)。
    /// </summary>
    private Transform CreateSingleFrame(string name, Vector3 center, float width, float height, Color? border = null)
    {
        Color col = border ?? frameColor;
        GameObject frameObj = new GameObject(name);
        frameObj.transform.SetParent(transform);
        frameObj.transform.position = center;

        // 背景（枠色を暗く落としたカード地。少し不透明に）
        CreateFramePart(frameObj.transform, "Background", Vector3.zero, width - borderWidth * 2f, height - borderWidth * 2f,
            new Color(col.r * 0.18f, col.g * 0.18f, col.b * 0.22f, 0.85f), -1);

        // 上の枠線
        CreateFramePart(frameObj.transform, "TopBorder", new Vector3(0f, height / 2f - borderWidth / 2f, 0f),
            width, borderWidth, col, 0);

        // 下の枠線
        CreateFramePart(frameObj.transform, "BottomBorder", new Vector3(0f, -height / 2f + borderWidth / 2f, 0f),
            width, borderWidth, col, 0);

        // 左の枠線
        CreateFramePart(frameObj.transform, "LeftBorder", new Vector3(-width / 2f + borderWidth / 2f, 0f, 0f),
            borderWidth, height, col, 0);

        // 右の枠線
        CreateFramePart(frameObj.transform, "RightBorder", new Vector3(width / 2f - borderWidth / 2f, 0f, 0f),
            borderWidth, height, col, 0);

        return frameObj.transform;
    }

    /// <summary>
    /// 味方枠の「窓」マスクを生成。ここに収まるキャラ(VisibleInsideMask)だけが描かれる。
    /// </summary>
    private SpriteMask CreateWindowMask(Transform frame, float innerW, float innerH)
    {
        var maskGo = new GameObject("Window", typeof(SpriteMask));
        maskGo.transform.SetParent(frame, false);
        maskGo.transform.localPosition = Vector3.zero;
        maskGo.transform.localScale = new Vector3(Mathf.Max(0.01f, innerW), Mathf.Max(0.01f, innerH), 1f);
        var mask = maskGo.GetComponent<SpriteMask>();
        mask.sprite = CreateWhiteSprite();
        return mask;
    }

    /// <summary>
    /// キャラを枠の窓にドックしてクリップする(下からはみ出さない窓表示)。
    /// BossIntro が枠へ配置し終えた直後に呼ぶ。
    /// 隣(特に同じ列の下)の枠への映り込みは、ポートレート高さの制限
    /// (<see cref="GetAllyPortraitPlacement"/>)で防いでいる。
    /// </summary>
    public void ClipCharacterToFrame(int index, Transform character)
    {
        if (index < 0 || index >= 4 || character == null) return;
        windowCharSrs[index] = character.GetComponentsInChildren<SpriteRenderer>(true);
        ApplyWindowClip(index, true);
    }

    /// <summary>枠 index のキャラのクリップ(VisibleInsideMask)を入/切する。</summary>
    private void ApplyWindowClip(int index, bool on)
    {
        var srs = windowCharSrs[index];
        if (srs == null) return;
        var mode = on ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null) srs[i].maskInteraction = mode;
    }

    /// <summary>ドック中の全キャラの窓クリップを一括で入/切する。</summary>
    private void SetAllWindowClipping(bool on)
    {
        for (int i = 0; i < 4; i++) ApplyWindowClip(i, on);
    }

    /// <summary>
    /// 指定プレイヤーの窓クリップを入/切する(公開)。
    /// clip=true で窓に収める(上半身のみ)、false で全体像(枠からはみ出して全身)。
    /// カード選択で「手番プレイヤーだけ全体像」を出すのに使う。
    /// </summary>
    public void SetPlayerWindowClip(int index, bool clip)
    {
        if (index < 0 || index >= 4) return;
        ApplyWindowClip(index, clip);
    }

    /// <summary>
    /// フレームのパーツ（枠線や背景）を生成
    /// </summary>
    private void CreateFramePart(Transform parent, string name, Vector3 localPos, float width, float height, Color color, int sortingOrder)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent);
        part.transform.localPosition = localPos;

        SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        // スケールでサイズを調整
        part.transform.localScale = new Vector3(width, height, 1f);
    }

    /// <summary>
    /// 白い1x1スプライトを生成
    /// </summary>
    private Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// 共有HPゲージを生成
    /// </summary>
    private void CreateSharedHpGauge(Vector3 center, float width, float height)
    {
        GameObject gaugeObj = new GameObject("SharedHpGauge");
        gaugeObj.transform.SetParent(transform);
        gaugeObj.transform.position = center;

        // 枠線（外側）
        float borderThickness = 0.03f;
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(gaugeObj.transform);
        borderObj.transform.localPosition = Vector3.zero;

        SpriteRenderer borderSr = borderObj.AddComponent<SpriteRenderer>();
        borderSr.sprite = CreateWhiteSprite();
        borderSr.color = hpGaugeBorderColor;
        borderSr.sortingOrder = 4;
        borderObj.transform.localScale = new Vector3(width, height, 1f);

        // 背景（暗いバー）
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(gaugeObj.transform);
        bgObj.transform.localPosition = Vector3.zero;

        SpriteRenderer bgSr = bgObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = CreateWhiteSprite();
        bgSr.color = hpGaugeBackgroundColor;
        bgSr.sortingOrder = 5;
        bgObj.transform.localScale = new Vector3(width - borderThickness * 2f, height - borderThickness * 2f, 1f);

        // HPゲージ本体（緑のバー）- 左端を基点にスケール
        float innerWidth = width - borderThickness * 4f;
        float innerHeight = height - borderThickness * 4f;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(gaugeObj.transform);
        // 左端に配置（初期状態では幅0）
        fillObj.transform.localPosition = new Vector3(-innerWidth / 2f, 0f, 0f);

        SpriteRenderer fillSr = fillObj.AddComponent<SpriteRenderer>();
        fillSr.sprite = CreateWhiteSprite();
        fillSr.color = hpGaugeFillColor;
        fillSr.sortingOrder = 6;
        // 初期状態は幅0
        fillObj.transform.localScale = new Vector3(0f, innerHeight, 1f);

        // 参照を保存
        sharedHpGauge = gaugeObj.transform;
        sharedHpFill = fillObj.transform;

        Debug.Log($"[BattleFrameUI] 共有HPゲージ生成完了: 幅={width}");
    }

    /// <summary>
    /// HPゲージ用パーティクルシステムを作成
    /// </summary>
    private void CreateHpParticleSystem(Vector3 center, float width, float height)
    {
        GameObject particleObj = new GameObject("HpParticles");
        particleObj.transform.SetParent(sharedHpGauge);
        particleObj.transform.localPosition = Vector3.zero;

        hpParticleSystem = particleObj.AddComponent<ParticleSystem>();

        // メインモジュール
        var main = hpParticleSystem.main;
        main.startLifetime = particleLifetime;
        main.startSpeed = particleSpeed;
        main.startSize = 0.08f;
        main.startColor = particleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.playOnAwake = false;

        // エミッション（初期はオフ）
        var emission = hpParticleSystem.emission;
        emission.enabled = false;

        // シェイプ（HPゲージに沿って水平に発生）
        var shape = hpParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width * 0.9f, height * 0.5f, 0.1f);

        // 速度の方向（上向き）- startVelocityを使用
        main.startSpeedMultiplier = 0f; // startSpeedは使わない
        var velocityOverLifetime = hpParticleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        // すべての軸で同じTwoConstants モードを使用
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(particleSpeed * 0.5f, particleSpeed);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // サイズの減衰
        var sizeOverLifetime = hpParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 色のフェードアウト
        var colorOverLifetime = hpParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(particleColor, 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // レンダラー設定
        var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 10;
        // デフォルトのスプライトを使用
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = particleColor;

        Debug.Log("[BattleFrameUI] HPゲージパーティクル生成完了");
    }

    /// <summary>
    /// HPゲージ回復パーティクルを再生
    /// </summary>
    public void PlayHpFillParticles()
    {
        if (hpParticleSystem != null)
        {
            hpParticleSystem.Emit(particleCount);
        }
    }

    /// <summary>
    /// HPゲージの現在位置でパーティクルを発生（連続再生用）
    /// </summary>
    public void EmitParticlesAtFillEdge(int count = 5)
    {
        if (hpParticleSystem == null || sharedHpFill == null)
        {
            return;
        }

        // ゲージの右端（伸びている先端）でパーティクルを発生
        Vector3 fillPos = sharedHpFill.position;
        float fillWidth = sharedHpFill.localScale.x;
        Vector3 emitPos = fillPos + new Vector3(fillWidth / 2f, 0f, 0f);

        var emitParams = new ParticleSystem.EmitParams();
        emitParams.position = emitPos;
        hpParticleSystem.Emit(emitParams, count);
    }

    /// <summary>
    /// 共有HPゲージの表示割合を設定（0〜1）
    /// </summary>
    public void SetSharedHpGaugeFill(float ratio)
    {
        if (sharedHpFill == null)
        {
            return;
        }

        ratio = Mathf.Clamp01(ratio);

        // ゲージ幅を計算
        float borderThickness = 0.03f;
        float innerWidth = sharedHpGaugeWidth - borderThickness * 4f;
        float fillWidth = innerWidth * ratio;

        Vector3 scale = sharedHpFill.localScale;
        scale.x = fillWidth;
        sharedHpFill.localScale = scale;

        // 左端を基点に伸ばすため、X位置を調整
        Vector3 pos = sharedHpFill.localPosition;
        pos.x = -innerWidth / 2f + fillWidth / 2f;
        sharedHpFill.localPosition = pos;
    }

    /// <summary>
    /// 味方キャラクターのフレーム中心座標を取得
    /// </summary>
    public Vector3 GetAllyFrameCenter(int index)
    {
        if (index < 0 || index >= AllyFrameBounds.Length)
        {
            Debug.LogError($"[BattleFrameUI] 無効なインデックス: {index}");
            return Vector3.zero;
        }
        return AllyFrameBounds[index].center;
    }

    /// <summary>
    /// ボスフレームの中心座標を取得
    /// </summary>
    public Vector3 GetBossFrameCenter()
    {
        return BossFrameBounds.center;
    }

    /// <summary>
    /// 四隅ポートレート配置: キャラを拡大し、頭を窓上端の少し下に合わせて、
    /// 下半身は窓の外(＝クリップ)へ出す。窓には上半身＋顔だけが見える。
    /// </summary>
    public void GetAllyPortraitPlacement(int index, Sprite sprite, out Vector3 pos, out float scale)
    {
        pos = (index >= 0 && index < AllyFrameBounds.Length) ? AllyFrameBounds[index].center : Vector3.zero;
        scale = 1f;
        if (sprite == null || index < 0 || index >= AllyFrameBounds.Length) return;

        Bounds fb = AllyFrameBounds[index];
        float innerW = fb.size.x - borderWidth * 2f;
        float innerH = fb.size.y - borderWidth * 2f;
        float spriteW = sprite.rect.width / sprite.pixelsPerUnit;
        float spriteH = sprite.rect.height / sprite.pixelsPerUnit;
        if (spriteW <= 0.0001f || spriteH <= 0.0001f) return;

        // 頭(スプライト上端)を置く高さ = 窓上端の少し下
        float windowTop = fb.center.y + innerH * 0.5f;
        float headroom = innerH * Mathf.Clamp01(portraitTopMargin);
        float spriteTop = windowTop - headroom;

        // 幅基準で拡大(窓幅を少し超える=上半身が窓を満たす)
        scale = (innerW / spriteW) * Mathf.Max(0.1f, portraitWidthFill);

        // 同じ列の「下の窓」に届かないよう、スプライト下端を制限してスケールを抑える。
        // (上段 P1/P2 の下は P3/P4。下段は下に窓が無いので制限なし)
        if (index < 2 && index + 2 < AllyFrameBounds.Length)
        {
            Bounds below = AllyFrameBounds[index + 2];
            float belowInnerH = below.size.y - borderWidth * 2f;
            float belowTop = below.center.y + belowInnerH * 0.5f;
            float bottomLimit = belowTop + innerH * 0.15f;      // 下窓の上端 + 余白
            float maxSpriteH = spriteTop - bottomLimit;         // ここに収まる最大高さ
            if (maxSpriteH > 0.01f)
            {
                float maxScale = maxSpriteH / spriteH;
                scale = Mathf.Min(scale, maxScale);
            }
        }

        // pivot を考慮して、スプライト上端が spriteTop に来るよう位置決め
        float pivotYNorm = sprite.pivot.y / Mathf.Max(1f, sprite.rect.height); // 0=下,1=上
        float topFromPos = spriteH * scale * (1f - pivotYNorm);                 // pos から上端まで
        pos = new Vector3(fb.center.x, spriteTop - topFromPos, 0f);
    }

    /// <summary>
    /// 味方キャラクターがフレームに収まるスケールを計算
    /// </summary>
    public float CalculateAllyScale(Sprite sprite, int frameIndex)
    {
        if (sprite == null || frameIndex < 0 || frameIndex >= AllyFrameBounds.Length)
        {
            return 1f;
        }

        Bounds frameBounds = AllyFrameBounds[frameIndex];
        float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
        float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;

        // フレームに収まるスケールを計算（マージンを考慮）
        float frameInnerWidth = frameBounds.size.x - borderWidth * 4f;
        float frameInnerHeight = frameBounds.size.y - borderWidth * 4f;

        float scaleX = frameInnerWidth / spriteWidth;
        float scaleY = frameInnerHeight / spriteHeight;

        return Mathf.Min(scaleX, scaleY) * 0.8f; // 80%のサイズで収める
    }

    /// <summary>
    /// ボスがフレームに収まるスケールを計算
    /// </summary>
    public float CalculateBossScale(Sprite sprite)
    {
        if (sprite == null)
        {
            return 1f;
        }

        float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
        float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;

        // フレームに収まるスケールを計算（マージンを考慮）
        float frameInnerWidth = BossFrameBounds.size.x - borderWidth * 4f;
        float frameInnerHeight = BossFrameBounds.size.y - borderWidth * 4f;

        float scaleX = frameInnerWidth / spriteWidth;
        float scaleY = frameInnerHeight / spriteHeight;

        return Mathf.Min(scaleX, scaleY) * 0.9f; // 90%のサイズで収める
    }

    // ======= カード選択演出用メソッド =======

    /// <summary>
    /// 全てのフレームを左端に集合させる
    /// </summary>
    public IEnumerator CollapseAllFramesToLeft(float duration, Transform[] characterTransforms = null)
    {
        Vector3[] startPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            startPositions[i] = allyFrames[i].position;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseInOutQuad(t);

            for (int i = 0; i < 4; i++)
            {
                Vector3 newPos = Vector3.Lerp(startPositions[i], collapseTargetPosition, easeT);
                allyFrames[i].position = newPos;

                // キャラクターもフレームと一緒に移動
                if (characterTransforms != null && i < characterTransforms.Length && characterTransforms[i] != null)
                {
                    characterTransforms[i].position = newPos;
                }
            }

            yield return null;
        }

        // 最終位置に確定
        for (int i = 0; i < 4; i++)
        {
            allyFrames[i].position = collapseTargetPosition;

            if (characterTransforms != null && i < characterTransforms.Length && characterTransforms[i] != null)
            {
                characterTransforms[i].position = collapseTargetPosition;
            }
        }
    }

    /// <summary>
    /// カード選択レイアウトに展開（対象キャラクターが左、カード3枚が右3つ分）
    /// </summary>
    public IEnumerator ExpandForCardSelect(int activePlayerIndex, float duration, Transform characterTransform = null)
    {
        // 展開先の位置を計算
        // 左端: 対象キャラクター（index 0の位置）
        // 右3つ: カード用スペース（index 1,2,3 の位置）

        Vector3[] targetPositions = new Vector3[4];

        // 対象プレイヤーは左端（インデックス0の元位置）に配置
        targetPositions[activePlayerIndex] = originalAllyPositions[0];

        // 他のプレイヤーは画面外左に隠す
        int cardSlot = 1; // カード用スロットは1から3
        for (int i = 0; i < 4; i++)
        {
            if (i != activePlayerIndex)
            {
                // 画面外に隠す（左端のさらに左）
                targetPositions[i] = collapseTargetPosition + new Vector3(-allyFrameWidth, 0f, 0f);
            }
        }

        Vector3[] startPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            startPositions[i] = allyFrames[i].position;
        }

        // キャラクターの開始位置（現在位置を使用）
        Vector3 charStartPos = characterTransform != null ? characterTransform.position : Vector3.zero;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutBack(t);

            for (int i = 0; i < 4; i++)
            {
                allyFrames[i].position = Vector3.Lerp(startPositions[i], targetPositions[i], easeT);
            }

            // キャラクターも左端（インデックス0の位置）に移動
            if (characterTransform != null)
            {
                // 対象キャラクターは常に左端（originalAllyPositions[0]）へ向かう
                characterTransform.position = Vector3.Lerp(charStartPos, originalAllyPositions[0], easeT);
            }

            yield return null;
        }

        // 最終位置に確定
        for (int i = 0; i < 4; i++)
        {
            allyFrames[i].position = targetPositions[i];
        }

        // キャラクターも最終位置に（左端の位置）
        if (characterTransform != null)
        {
            characterTransform.position = originalAllyPositions[0];
        }
    }

    /// <summary>
    /// キャラクターをフレームと一緒に左へ退場させる
    /// </summary>
    public IEnumerator CollapseFrameWithCharacter(int frameIndex, float duration, Transform characterTransform = null)
    {
        if (frameIndex < 0 || frameIndex >= 4) yield break;

        // フレームの開始位置と目標位置
        Vector3 frameStartPos = allyFrames[frameIndex].position;
        Vector3 targetPos = collapseTargetPosition + new Vector3(-allyFrameWidth * 2f, 0f, 0f);  // 画面外左へ

        // キャラクターは左端（originalAllyPositions[0]）からスタート
        Vector3 charStartPos = characterTransform != null ? characterTransform.position : originalAllyPositions[0];

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseInOutQuad(t);

            Vector3 currentFramePos = Vector3.Lerp(frameStartPos, targetPos, easeT);
            allyFrames[frameIndex].position = currentFramePos;

            // キャラクターも左へ退場（現在位置から目標位置へ）
            if (characterTransform != null)
            {
                characterTransform.position = Vector3.Lerp(charStartPos, targetPos, easeT);
            }

            yield return null;
        }

        allyFrames[frameIndex].position = targetPos;
        if (characterTransform != null)
        {
            characterTransform.position = targetPos;
        }
    }

    /// <summary>
    /// カード用スロットの位置を取得（右3つのフレーム位置）
    /// </summary>
    public Vector3[] GetCardSlotPositions()
    {
        return new Vector3[]
        {
            originalAllyPositions[1],  // カード1
            originalAllyPositions[2],  // カード2
            originalAllyPositions[3],  // カード3
        };
    }

    /// <summary>
    /// カードフレームのサイズを取得
    /// </summary>
    public Vector2 GetAllyFrameSize()
    {
        return new Vector2(allyFrameWidth, allyFrameHeight);
    }

    /// <summary>
    /// 集合位置（画面外左端）を取得
    /// </summary>
    public Vector3 GetCollapseTargetPosition()
    {
        return collapseTargetPosition;
    }

    /// <summary>
    /// 全てのフレームを非表示にする
    /// </summary>
    public void HideAllFrames()
    {
        HideAllyFramesOnly();

        // ボスフレームも非表示
        if (bossFrame != null)
        {
            bossFrame.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 全てのフレームを表示する
    /// </summary>
    public void ShowAllFrames()
    {
        ShowAllyFramesOnly();

        // ボスフレームも表示
        if (bossFrame != null)
        {
            bossFrame.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// プレイヤーフレームのみを非表示にする（ボスは残す）
    /// </summary>
    public void HideAllyFramesOnly()
    {
        // 枠(＝窓マスク)を消す間はキャラを全身表示に戻す(カード選択のズームで全体が見えるように)
        SetAllWindowClipping(false);
        for (int i = 0; i < 4; i++)
        {
            if (allyFrames[i] != null)
            {
                allyFrames[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 味方枠を「フェード＋わずかに拡大」でカッコよく消す(カード選択突入時の演出)。
    /// 消えた後は色・スケールを元に戻して非表示にする(次に表示する時のため)。
    /// </summary>
    public IEnumerator HideAllyFramesAnimated(float duration)
    {
        var frameSrs = new SpriteRenderer[4][];
        var baseCols = new Color[4][];
        var baseScales = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            if (allyFrames[i] == null) { frameSrs[i] = null; continue; }
            var srs = allyFrames[i].GetComponentsInChildren<SpriteRenderer>(true); // SpriteMask は含まれない
            frameSrs[i] = srs;
            var cols = new Color[srs.Length];
            for (int j = 0; j < srs.Length; j++) cols[j] = (srs[j] != null) ? srs[j].color : Color.white;
            baseCols[i] = cols;
            baseScales[i] = allyFrames[i].localScale;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float a = 1f - p * p;         // フェードアウト(イーズイン)
            float sc = 1f + 0.18f * p;    // ふわっと拡大しながら消える
            for (int i = 0; i < 4; i++)
            {
                if (frameSrs[i] == null || allyFrames[i] == null) continue;
                allyFrames[i].localScale = baseScales[i] * sc;
                var srs = frameSrs[i];
                var cols = baseCols[i];
                for (int j = 0; j < srs.Length; j++)
                {
                    if (srs[j] == null) continue;
                    Color c = cols[j];
                    c.a *= a;
                    srs[j].color = c;
                }
            }
            yield return null;
        }

        // 後始末: 非表示にして色・スケールを元へ(再表示に備える)
        for (int i = 0; i < 4; i++)
        {
            if (allyFrames[i] == null) continue;
            allyFrames[i].gameObject.SetActive(false);
            allyFrames[i].localScale = baseScales[i];
            var srs = frameSrs[i];
            var cols = baseCols[i];
            if (srs != null)
                for (int j = 0; j < srs.Length; j++)
                    if (srs[j] != null) srs[j].color = cols[j];
        }
        SetAllWindowClipping(false);
    }

    /// <summary>
    /// プレイヤーフレームのみを表示する
    /// </summary>
    public void ShowAllyFramesOnly()
    {
        for (int i = 0; i < 4; i++)
        {
            if (allyFrames[i] != null)
            {
                // 元の位置に戻す
                allyFrames[i].position = originalAllyPositions[i];
            }
        }
        // 人数に応じた表示/グレーアウト(ソロは1枠のみ表示、複数は空席をグレーアウト)
        ApplySlotVisibility();
        // 枠が出ている間はキャラを窓にクリップ(下からはみ出さない)
        SetAllWindowClipping(true);
    }

    /// <summary>
    /// 全てのフレームを元の位置に戻す
    /// </summary>
    public IEnumerator RestoreAllFrames(float duration, Transform[] characterTransforms = null, Vector3[] originalCharacterPositions = null)
    {
        Vector3[] startPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            startPositions[i] = allyFrames[i].position;
        }

        // キャラクターの開始位置も保存
        Vector3[] charStartPositions = null;
        if (characterTransforms != null)
        {
            charStartPositions = new Vector3[characterTransforms.Length];
            for (int i = 0; i < characterTransforms.Length; i++)
            {
                if (characterTransforms[i] != null)
                {
                    charStartPositions[i] = characterTransforms[i].position;
                }
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutBack(t);

            for (int i = 0; i < 4; i++)
            {
                allyFrames[i].position = Vector3.Lerp(startPositions[i], originalAllyPositions[i], easeT);

                // キャラクターも元の位置に戻す
                if (characterTransforms != null && i < characterTransforms.Length && characterTransforms[i] != null)
                {
                    if (originalCharacterPositions != null && i < originalCharacterPositions.Length)
                    {
                        characterTransforms[i].position = Vector3.Lerp(charStartPositions[i], originalCharacterPositions[i], easeT);
                    }
                    else
                    {
                        characterTransforms[i].position = allyFrames[i].position;
                    }
                }
            }

            yield return null;
        }

        // 最終位置に確定
        for (int i = 0; i < 4; i++)
        {
            allyFrames[i].position = originalAllyPositions[i];

            if (characterTransforms != null && i < characterTransforms.Length && characterTransforms[i] != null)
            {
                if (originalCharacterPositions != null && i < originalCharacterPositions.Length)
                {
                    characterTransforms[i].position = originalCharacterPositions[i];
                }
                else
                {
                    characterTransforms[i].position = originalAllyPositions[i];
                }
            }
        }
    }

    /// <summary>
    /// フレームを即座に元の位置に戻す
    /// </summary>
    public void RestoreAllFramesImmediate()
    {
        for (int i = 0; i < 4; i++)
        {
            if (allyFrames[i] != null)
            {
                allyFrames[i].position = originalAllyPositions[i];
            }
        }
    }

    /// <summary>
    /// 全てのフレームとキャラクターを画面外左に配置（即座に）
    /// </summary>
    public void SetAllFramesToLeftOffscreen(Transform[] characterTransforms = null)
    {
        Vector3 offscreenPosition = collapseTargetPosition + new Vector3(-allyFrameWidth * 2f, 0f, 0f);

        for (int i = 0; i < 4; i++)
        {
            if (allyFrames[i] != null)
            {
                allyFrames[i].position = offscreenPosition;
            }

            if (characterTransforms != null && i < characterTransforms.Length && characterTransforms[i] != null)
            {
                characterTransforms[i].position = offscreenPosition;
            }
        }
    }

    /// <summary>
    /// 全てのフレームとキャラクターを画面外左から元の位置へアニメーションで戻す
    /// </summary>
    public IEnumerator RestoreAllFramesFromLeft(float duration, Transform[] characterTransforms = null, Vector3[] originalCharacterPositions = null)
    {
        Vector3[] startPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            startPositions[i] = allyFrames[i].position;
        }

        // キャラクターの開始位置も保存
        Vector3[] charStartPositions = null;
        if (characterTransforms != null)
        {
            charStartPositions = new Vector3[characterTransforms.Length];
            for (int i = 0; i < characterTransforms.Length; i++)
            {
                if (characterTransforms[i] != null)
                {
                    charStartPositions[i] = characterTransforms[i].position;
                }
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutBack(t);

            for (int i = 0; i < 4; i++)
            {
                allyFrames[i].position = Vector3.Lerp(startPositions[i], originalAllyPositions[i], easeT);

                // キャラクターも元の位置に戻す
                if (characterTransforms != null && i < characterTransforms.Length && characterTransforms[i] != null)
                {
                    if (originalCharacterPositions != null && i < originalCharacterPositions.Length)
                    {
                        characterTransforms[i].position = Vector3.Lerp(charStartPositions[i], originalCharacterPositions[i], easeT);
                    }
                    else
                    {
                        characterTransforms[i].position = allyFrames[i].position;
                    }
                }
            }

            yield return null;
        }

        // 最終位置に確定
        for (int i = 0; i < 4; i++)
        {
            allyFrames[i].position = originalAllyPositions[i];

            if (characterTransforms != null && i < characterTransforms.Length && characterTransforms[i] != null)
            {
                if (originalCharacterPositions != null && i < originalCharacterPositions.Length)
                {
                    characterTransforms[i].position = originalCharacterPositions[i];
                }
                else
                {
                    characterTransforms[i].position = originalAllyPositions[i];
                }
            }
        }
    }

    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ======= カメラズーム演出用 =======

    private float originalOrthographicSize;
    private Vector3 originalCameraPosition;
    private bool cameraZoomed = false;

    /// <summary>
    /// カメラをズームしてキャラクターとカードにフォーカス
    /// </summary>
    public IEnumerator ZoomCameraToCardSelect(float duration)
    {
        if (mainCamera == null) yield break;

        // 元の状態を保存
        if (!cameraZoomed)
        {
            originalOrthographicSize = mainCamera.orthographicSize;
            originalCameraPosition = mainCamera.transform.position;
            cameraZoomed = true;
        }

        // ズーム先の設定
        // キャラクター（左端）と3枚のカードを含む領域にフォーカス
        Vector3[] cardSlots = GetCardSlotPositions();
        Vector3 leftFrameCenter = originalAllyPositions[0];

        // フォーカスの中心点（キャラクターと3枚のカードの中間）
        float focusX = (leftFrameCenter.x + cardSlots[2].x) / 2f;
        float focusY = leftFrameCenter.y;
        Vector3 targetCameraPos = new Vector3(focusX, focusY, originalCameraPosition.z);

        // ズーム量（キャラクターとカード3枚が収まるサイズ）
        float targetSize = originalOrthographicSize * 0.55f;

        float elapsed = 0f;
        float startSize = mainCamera.orthographicSize;
        Vector3 startPos = mainCamera.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutQuad(t);

            mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, easeT);
            mainCamera.transform.position = Vector3.Lerp(startPos, targetCameraPos, easeT);

            yield return null;
        }

        mainCamera.orthographicSize = targetSize;
        mainCamera.transform.position = targetCameraPos;
    }

    /// <summary>
    /// カメラを元の状態に戻す
    /// </summary>
    public IEnumerator RestoreCameraZoom(float duration)
    {
        if (mainCamera == null || !cameraZoomed) yield break;

        float elapsed = 0f;
        float startSize = mainCamera.orthographicSize;
        Vector3 startPos = mainCamera.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutQuad(t);

            mainCamera.orthographicSize = Mathf.Lerp(startSize, originalOrthographicSize, easeT);
            mainCamera.transform.position = Vector3.Lerp(startPos, originalCameraPosition, easeT);

            yield return null;
        }

        mainCamera.orthographicSize = originalOrthographicSize;
        mainCamera.transform.position = originalCameraPosition;
        cameraZoomed = false;
    }

    /// <summary>
    /// カメラを即座に元の状態に戻す
    /// </summary>
    public void RestoreCameraZoomImmediate()
    {
        if (mainCamera == null || !cameraZoomed) return;

        mainCamera.orthographicSize = originalOrthographicSize;
        mainCamera.transform.position = originalCameraPosition;
        cameraZoomed = false;
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    // ======= スポットライト演出用 =======

    private GameObject spotlightOverlay;
    private GameObject spotlightCircle;

    /// <summary>
    /// スポットライト演出を開始（暗いビネット効果）
    /// </summary>
    public void ShowSpotlight(Vector3 targetPosition, float radius = 1.5f)
    {
        // 既存のスポットライトを削除
        HideSpotlight();

        // 画面の四隅を暗くするビネット効果
        float camHeight = mainCamera.orthographicSize * 2f;
        float camWidth = camHeight * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;

        spotlightOverlay = new GameObject("VignetteOverlay");
        spotlightOverlay.transform.SetParent(transform);
        spotlightOverlay.transform.position = new Vector3(camPos.x, camPos.y, -0.5f);

        var sr = spotlightOverlay.AddComponent<SpriteRenderer>();
        sr.sprite = CreateVignetteSprite();
        sr.sortingOrder = 50;
        spotlightOverlay.transform.localScale = new Vector3(camWidth * 1.5f, camHeight * 1.5f, 1f);
    }

    /// <summary>
    /// スポットライト演出を非表示
    /// </summary>
    public void HideSpotlight()
    {
        if (spotlightOverlay != null)
        {
            Destroy(spotlightOverlay);
            spotlightOverlay = null;
        }
        if (spotlightCircle != null)
        {
            Destroy(spotlightCircle);
            spotlightCircle = null;
        }
    }

    /// <summary>
    /// スポットライトの位置を更新
    /// </summary>
    public void UpdateSpotlightPosition(Vector3 targetPosition)
    {
        if (spotlightCircle != null)
        {
            spotlightCircle.transform.position = new Vector3(targetPosition.x, targetPosition.y, -1.5f);
        }
    }

    /// <summary>
    /// ビネット効果用スプライトを生成（四隅が暗く中央は明るい）
    /// </summary>
    private Sprite CreateVignetteSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        float center = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 中央からの距離（正規化）
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // ビネット効果：中央は透明、端に向かって暗くなる
                float alpha = Mathf.Clamp01((dist - 0.3f) * 0.6f);
                alpha = alpha * alpha * 0.5f; // より自然なフォールオフ、最大50%の暗さ

                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// 穴あきスポットライトマスクを生成（中央が透明、周りが暗い）
    /// </summary>
    private Sprite CreateSpotlightMaskSprite(float camWidth, float camHeight, float holeRadius)
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        float center = size / 2f;

        // 穴の半径（テクスチャサイズに対する比率）
        float holeRadiusPixels = size * 0.15f; // 小さめの穴
        float fadeWidth = size * 0.08f; // フェードの幅

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                float alpha;
                if (dist < holeRadiusPixels)
                {
                    // 穴の中は透明
                    alpha = 0f;
                }
                else if (dist < holeRadiusPixels + fadeWidth)
                {
                    // フェード領域
                    float t = (dist - holeRadiusPixels) / fadeWidth;
                    alpha = t * t * 0.75f; // 暗さ最大0.75
                }
                else
                {
                    // 周りは暗い
                    alpha = 0.75f;
                }

                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        // 画面全体をカバーするサイズ
        float maxDim = Mathf.Max(camWidth, camHeight) * 2f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / maxDim);
    }

    /// <summary>
    /// スポットライトのグロー効果用スプライトを生成
    /// </summary>
    private Sprite CreateSpotlightGlowSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        float center = size / 2f;
        float innerRadius = size * 0.35f;
        float outerRadius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                float alpha = 0f;
                if (dist >= innerRadius && dist <= outerRadius)
                {
                    // リング状のグロー
                    float t = 1f - Mathf.Abs(dist - (innerRadius + outerRadius) / 2f) / ((outerRadius - innerRadius) / 2f);
                    alpha = t * t;
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
