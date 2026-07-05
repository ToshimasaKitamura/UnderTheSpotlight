using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 部位破壊時の視覚演出を管理するコンポーネント。
/// EnemyRuntimeState.OnPartDestroyed イベントを購読し、
/// 破壊演出(フラッシュ、テキスト表示、画面揺れ等)を実行する。
/// </summary>
public class PartDestroyEffectUI : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("演出の表示時間(秒)")]
    public float effectDuration = 1.2f;
    [Tooltip("画面フラッシュの色")]
    public Color flashColor = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("画面揺れの強さ")]
    public float shakeIntensity = 10f;

    private static PartDestroyEffectUI instance;
    public static PartDestroyEffectUI Instance => instance;

    private List<DestroyEffect> activeEffects = new List<DestroyEffect>();
    private float flashAlpha = 0f;
    private Vector3 originalCamPos;
    private bool isShaking = false;

    private class DestroyEffect
    {
        public string partName;
        public string effectName;
        public float startTime;
        public float duration;
        public Color color;
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        // EnemyRuntimeState のイベントを購読
        var enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (enemyState != null)
        {
            enemyState.OnPartDestroyed += OnPartDestroyed;
        }
    }

    private void OnPartDestroyed(EnemyPart part)
    {
        if (part == null) return;

        // 演出を追加
        var effect = new DestroyEffect
        {
            partName = part.displayName ?? part.id,
            effectName = GetEffectDescription(part.destructionEffect),
            startTime = Time.time,
            duration = effectDuration,
            color = GetEffectColor(part.destructionEffect)
        };
        activeEffects.Add(effect);

        // 画面フラッシュ
        StartCoroutine(DoFlash());

        // 画面揺れ
        StartCoroutine(DoShake());

        Debug.Log($"[PartDestroyEffectUI] 部位破壊演出: {part.displayName} - {effect.effectName}");
    }

    private string GetEffectDescription(PartDestructionEffect effect)
    {
        switch (effect)
        {
            case PartDestructionEffect.WeakenNextAttack:
                return "次の攻撃弱体化!";
            case PartDestructionEffect.PreventHeal:
                return "回復封印!";
            case PartDestructionEffect.ReducePatternDensity:
                return "弾幕密度低下!";
            case PartDestructionEffect.ReducePatternSpeed:
                return "弾速低下!";
            case PartDestructionEffect.DisableMovement:
                return "移動封印!";
            default:
                return "BREAK!";
        }
    }

    private Color GetEffectColor(PartDestructionEffect effect)
    {
        switch (effect)
        {
            case PartDestructionEffect.WeakenNextAttack:
                return new Color(1f, 0.5f, 0.2f);
            case PartDestructionEffect.PreventHeal:
                return new Color(0.8f, 0.2f, 0.8f);
            case PartDestructionEffect.ReducePatternDensity:
                return new Color(0.2f, 0.8f, 1f);
            case PartDestructionEffect.ReducePatternSpeed:
                return new Color(0.2f, 1f, 0.5f);
            case PartDestructionEffect.DisableMovement:
                return new Color(1f, 1f, 0.2f);
            default:
                return Color.white;
        }
    }

    private IEnumerator DoFlash()
    {
        flashAlpha = 0.6f;
        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            flashAlpha = Mathf.Lerp(0.6f, 0f, elapsed / duration);
            yield return null;
        }
        flashAlpha = 0f;
    }

    private IEnumerator DoShake()
    {
        if (isShaking) yield break;
        isShaking = true;

        Camera cam = Camera.main;
        if (cam == null) { isShaking = false; yield break; }

        originalCamPos = cam.transform.position;
        float elapsed = 0f;
        float duration = 0.25f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = shakeIntensity * (1f - elapsed / duration);
            Vector3 offset = new Vector3(
                Random.Range(-1f, 1f) * strength * 0.01f,
                Random.Range(-1f, 1f) * strength * 0.01f,
                0f
            );
            cam.transform.position = originalCamPos + offset;
            yield return null;
        }

        cam.transform.position = originalCamPos;
        isShaking = false;
    }

    void OnGUI()
    {
        // 画面フラッシュ
        if (flashAlpha > 0.01f)
        {
            Color c = flashColor;
            c.a = flashAlpha;
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 破壊演出テキスト
        float currentTime = Time.time;
        int pixelScale = Mathf.Max(3, Screen.height / 240);

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            float elapsed = currentTime - effect.startTime;

            if (elapsed >= effect.duration)
            {
                activeEffects.RemoveAt(i);
                continue;
            }

            float progress = elapsed / effect.duration;
            float alpha = 1f;
            if (progress < 0.1f)
                alpha = progress / 0.1f;
            else if (progress > 0.7f)
                alpha = 1f - (progress - 0.7f) / 0.3f;

            // 部位名表示
            float yOffset = -50f * progress;
            DrawEffectText(effect.partName, Screen.height * 0.35f + yOffset, effect.color, alpha, pixelScale + 2);

            // 効果名表示
            DrawEffectText(effect.effectName, Screen.height * 0.42f + yOffset, effect.color, alpha, pixelScale + 1);
        }
    }

    private void DrawEffectText(string text, float y, Color color, float alpha, int scale)
    {
        var tex = PixelFontRenderer.RenderText(text, color, scale);
        if (tex != null)
        {
            float x = (Screen.width - tex.width) / 2f;
            Color c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
            GUI.color = c;
            // テクスチャは毎フレーム作り直しているので解放
            // (本来はキャッシュすべきだが簡易実装)
            Destroy(tex);
        }
    }
}
