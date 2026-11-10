using System.Collections;
using UnityEngine;

/// <summary>
/// 共有HPバー UI（8bit ピクセル風）。
/// OnGUI ベースで描画し、PixelFontRenderer を使用。
/// SharedHpController.OnChanged から呼ばれる想定。
/// </summary>
public class SharedHpGaugeUI : MonoBehaviour
{
    [Tooltip("バーの幅(px)。")]
    public float barWidth = 600f;
    [Tooltip("バーの高さ(px)。")]
    public float barHeight = 36f;
    [Tooltip("画面下端からのオフセット(px)。")]
    public float bottomOffset = 40f;
    [Tooltip("HP残量側の色(緑)。")]
    public Color fillColor = new Color(0.4f, 0.85f, 0.5f, 1f);        // HpFull
    [Tooltip("背景色(暗紫)。")]
    public Color backgroundColor = new Color(0.1f, 0.08f, 0.12f, 0.9f); // HpBackground
    [Tooltip("枠線の色(紫)。")]
    public Color borderColor = new Color(0.5f, 0.35f, 0.6f, 1f);      // FrameOuter
    [Tooltip("危険時(HP 25%以下)の色(赤)。")]
    public Color dangerColor = new Color(1f, 0.35f, 0.3f, 1f);        // HpDanger

    [Tooltip("購読対象の HP コントローラ。未設定時は FindObjectOfType で探索。")]
    public SharedHpController source;

    private int currentHp;
    private int maxHp;
    private bool isVisible = false;

    // ピクセルテクスチャ
    private Texture2D bgTexture;
    private Texture2D fillTexture;
    private Texture2D dangerTexture;
    private Texture2D labelTexture;
    private int lastDisplayedHp = -1;
    private int lastDisplayedMax = -1;

    // 点滅制御(危険時)
    private float blinkTimer;
    private bool blinkOn = true;

    void Awake()
    {
        if (source == null) source = FindObjectOfType<SharedHpController>();
        if (source != null)
        {
            source.OnChanged += HandleChanged;
            currentHp = source.CurrentHp;
            maxHp = source.maxHp;
        }
        // 味方HPはオレンジに固定(シーン保存値に左右されないようコードで上書き)。
        fillColor = new Color(1f, 0.55f, 0.12f, 1f);   // オレンジ
        dangerColor = new Color(1f, 0.35f, 0.1f, 1f);  // 低HP時は濃いオレンジ寄りの赤
        CreateTextures();
    }

    /// <summary>マップ画面など戦闘外で非表示にするための切替。WorldMapPhase が呼ぶ。</summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (visible) { StopFade(); renderAlpha = 1f; }   // 再表示時は不透明に戻す
    }

    // ---- フェードアウト(弾幕フレームが閉じるのに合わせて消す) ----
    private float renderAlpha = 1f;   // 全描画に掛ける透明度(1=不透明)
    private Coroutine fadeRoutine;

    /// <summary>指定秒でフェードインして表示する(弾幕開始の舞台が整ってから)。</summary>
    public void FadeIn(float duration)
    {
        StopFade();
        isVisible = true;
        if (!gameObject.activeInHierarchy || duration <= 0f) { renderAlpha = 1f; return; }
        renderAlpha = 0f;
        fadeRoutine = StartCoroutine(FadeInCo(duration));
    }

    private IEnumerator FadeInCo(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            renderAlpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        renderAlpha = 1f;
        fadeRoutine = null;
    }

    /// <summary>指定秒でフェードアウトして非表示にする(弾幕終了演出に同期)。</summary>
    public void FadeOutAndHide(float duration)
    {
        if (!isVisible) return;
        if (!gameObject.activeInHierarchy || duration <= 0f) { isVisible = false; renderAlpha = 1f; return; }
        StopFade();
        fadeRoutine = StartCoroutine(FadeOutCo(duration));
    }

    private void StopFade()
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
    }

    private IEnumerator FadeOutCo(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            renderAlpha = Mathf.Clamp01(1f - t / dur);
            yield return null;
        }
        isVisible = false;
        renderAlpha = 1f;   // 次回表示のためリセット
        fadeRoutine = null;
    }

    void OnDestroy()
    {
        if (source != null) source.OnChanged -= HandleChanged;
        DestroyTextures();
    }

    // ダメージ演出(削れた分が光って消える＋ゲージが揺れる)
    private const float DamageEffectDuration = 0.45f;
    private float damageEffectTimer;
    private float ghostFromRatio;  // 旧HP比率
    private float ghostToRatio;    // 新HP比率

    private void HandleChanged(int cur, int max)
    {
        if (max > 0 && cur < currentHp)
        {
            // 減少 → ダメージ演出を発火(削れた区間を記録)
            ghostFromRatio = Mathf.Clamp01((float)currentHp / max);
            ghostToRatio = Mathf.Clamp01((float)cur / max);
            damageEffectTimer = DamageEffectDuration;
        }
        currentHp = cur;
        maxHp = max;
    }

    public void SetHp(int current, int max)
    {
        currentHp = current;
        maxHp = max;
    }

    private void CreateTextures()
    {
        // 背景(暗いピクセルボックス)
        bgTexture = PixelFontRenderer.CreatePixelBox(
            (int)barWidth / 4, (int)barHeight / 4,
            backgroundColor, borderColor, 1);

        // HP残量バー
        fillTexture = CreateSolidPixelTexture(fillColor);
        dangerTexture = CreateSolidPixelTexture(dangerColor);
    }

    private Texture2D CreateSolidPixelTexture(Color color)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void DestroyTextures()
    {
        if (bgTexture != null) Destroy(bgTexture);
        if (fillTexture != null) Destroy(fillTexture);
        if (dangerTexture != null) Destroy(dangerTexture);
        if (labelTexture != null) Destroy(labelTexture);
    }

    void Update()
    {
        // 危険時の点滅
        float ratio = (maxHp > 0) ? (float)currentHp / maxHp : 0f;
        if (ratio <= 0.25f && ratio > 0f)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= 0.15f)
            {
                blinkTimer = 0f;
                blinkOn = !blinkOn;
            }
        }
        else
        {
            blinkOn = true;
            blinkTimer = 0f;
        }

        if (damageEffectTimer > 0f) damageEffectTimer -= Time.deltaTime;

        // ラベルテクスチャの更新
        if (currentHp != lastDisplayedHp || maxHp != lastDisplayedMax)
        {
            UpdateLabelTexture();
            lastDisplayedHp = currentHp;
            lastDisplayedMax = maxHp;
        }
    }

    private void UpdateLabelTexture()
    {
        if (labelTexture != null) Destroy(labelTexture);
        string text = $"HP {currentHp}/{maxHp}";
        labelTexture = PixelFontRenderer.RenderText(text, Color.white, 2);
    }

    void OnGUI()
    {
        if (!isVisible || renderAlpha <= 0f) return;

        // フェード用: 全描画にかかる透明度を GUI.color に乗せる(終了時に復元)
        Color guiColorOrig = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, renderAlpha);

        // 画面中央下にバーを配置
        float x = (Screen.width - barWidth) / 2f;
        float y = Screen.height - bottomOffset - barHeight;

        // ダメージ中はゲージを横に揺らす(減衰する振動)
        if (damageEffectTimer > 0f)
        {
            float pShake = damageEffectTimer / DamageEffectDuration;
            x += Mathf.Sin(damageEffectTimer * 90f) * 6f * pShake;
        }

        // 背景
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), bgTexture, ScaleMode.StretchToFill);

        // HP残量バー
        float ratio = (maxHp > 0) ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        float padding = 4f;
        float fillWidth = (barWidth - padding * 2) * ratio;
        float fillHeight = barHeight - padding * 2;

        if (fillWidth > 0)
        {
            bool isDanger = ratio <= 0.25f;
            Texture2D barTex = isDanger ? dangerTexture : fillTexture;

            // 危険時はバーを消さず色の明滅で警告する。
            // (フィルを描画ON/OFFすると残量が「1↔0」に点滅して見えるため、アルファのみ明滅させる)
            Color prevColor = GUI.color;
            if (isDanger) GUI.color = new Color(1f, 1f, 1f, (blinkOn ? 1f : 0.5f) * renderAlpha);
            GUI.DrawTexture(
                new Rect(x + padding, y + padding, fillWidth, fillHeight),
                barTex, ScaleMode.StretchToFill);
            GUI.color = prevColor;
        }

        // ダメージ演出: 削れた区間(新HP→旧HP)を白→赤で光らせてフェードアウト
        if (damageEffectTimer > 0f && maxHp > 0)
        {
            float p = Mathf.Clamp01(damageEffectTimer / DamageEffectDuration);
            float innerW = barWidth - padding * 2;
            float gx = x + padding + innerW * ghostToRatio;
            float gw = innerW * Mathf.Max(0f, ghostFromRatio - ghostToRatio);
            if (gw > 0.5f)
            {
                Color prevColor = GUI.color;
                Color ghost = Color.Lerp(new Color(1f, 0.35f, 0.3f, 1f), Color.white, p); // 序盤=白→終盤=赤
                ghost.a = p * renderAlpha;
                GUI.color = ghost;
                GUI.DrawTexture(new Rect(gx, y + padding, gw, fillHeight), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }
        }

        // 区切り線(最大HP分割 = 5段階)。各HP境界に縦線を引いて段階を見せる。
        if (maxHp > 1)
        {
            float innerW = barWidth - padding * 2;
            float segW = innerW / maxHp;
            Color prevColor = GUI.color;
            GUI.color = new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * renderAlpha);
            for (int i = 1; i < maxHp; i++)
            {
                float lx = x + padding + segW * i - 1f;
                GUI.DrawTexture(new Rect(lx, y + padding, 2f, fillHeight), Texture2D.whiteTexture);
            }
            GUI.color = prevColor;
        }

        // ラベル(中央)
        if (labelTexture != null)
        {
            float labelX = x + (barWidth - labelTexture.width) / 2f;
            float labelY = y + (barHeight - labelTexture.height) / 2f;
            GUI.DrawTexture(new Rect(labelX, labelY, labelTexture.width, labelTexture.height), labelTexture);
        }

        GUI.color = guiColorOrig;   // IMGUI のグローバル色を復元
    }
}
