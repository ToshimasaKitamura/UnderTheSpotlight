using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP 2D Renderer用のGlobal Light 2Dを自動設定するコンポーネント。
/// シーンにGlobal Light 2Dがない場合、自動的に生成する。
/// これにより、Sprite Lit 2D Materialを使用するスプライトが正しく表示される。
/// </summary>
public class GlobalLight2DSetup : MonoBehaviour
{
    [Tooltip("ライトの強度（1.0 = 通常の明るさ）")]
    public float intensity = 1f;

    [Tooltip("ライトの色")]
    public Color lightColor = Color.white;

    private void Awake()
    {
        EnsureGlobalLight2D();
    }

    /// <summary>
    /// シーンにGlobal Light 2Dが存在しない場合、生成する
    /// </summary>
    public void EnsureGlobalLight2D()
    {
        // 既存のLight2Dを検索
        var existingLights = FindObjectsOfType<Light2D>();

        foreach (var light in existingLights)
        {
            if (light.lightType == Light2D.LightType.Global)
            {
                Debug.Log($"[GlobalLight2DSetup] 既存のGlobal Light 2Dを発見: {light.gameObject.name}");
                return;
            }
        }

        // Global Light 2Dが存在しないので生成
        CreateGlobalLight2D();
    }

    private void CreateGlobalLight2D()
    {
        var lightObj = new GameObject("Global Light 2D");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = Vector3.zero;

        var light2d = lightObj.AddComponent<Light2D>();
        light2d.lightType = Light2D.LightType.Global;
        light2d.intensity = intensity;
        light2d.color = lightColor;

        Debug.Log($"[GlobalLight2DSetup] Global Light 2Dを生成しました (intensity={intensity}, color={lightColor})");
    }

    /// <summary>
    /// エディタ用：既存のLight2Dの状態をログ出力
    /// </summary>
    [ContextMenu("Debug: Check Light2D Status")]
    public void DebugCheckLightStatus()
    {
        var lights = FindObjectsOfType<Light2D>();
        Debug.Log($"[GlobalLight2DSetup] シーン内のLight2D: {lights.Length}個");

        foreach (var light in lights)
        {
            Debug.Log($"  - {light.gameObject.name}: type={light.lightType}, intensity={light.intensity}, color={light.color}");
        }

        if (lights.Length == 0)
        {
            Debug.LogWarning("[GlobalLight2DSetup] Light2Dが見つかりません！スプライトが暗く表示される可能性があります。");
        }
    }
}
