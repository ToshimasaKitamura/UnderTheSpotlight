using UnityEngine;

/// <summary>
/// カメラのレンダリング状態をデバッグし、必要に応じて修正するコンポーネント
/// </summary>
public class CameraDebugger : MonoBehaviour
{
    private void Awake()
    {
        // カメラのZ位置を修正（2DゲームではZ=-10が標準）
        FixCameraZPosition();
    }

    private void Start()
    {
        DebugCameraState();
    }

    /// <summary>
    /// カメラのZ位置が0付近の場合、-10に修正する
    /// </summary>
    private void FixCameraZPosition()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 pos = cam.transform.position;
        if (Mathf.Abs(pos.z) < 1f)
        {
            Debug.LogWarning($"[CameraDebugger] カメラのZ位置が{pos.z}でした。-10に修正します。");
            cam.transform.position = new Vector3(pos.x, pos.y, -10f);
        }
    }

    [ContextMenu("Debug Camera State")]
    public void DebugCameraState()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[CameraDebugger] Main Cameraが見つかりません！");
            return;
        }

        Debug.Log($"[CameraDebugger] === カメラ状態 ===");
        Debug.Log($"  名前: {cam.gameObject.name}");
        Debug.Log($"  位置: {cam.transform.position}");
        Debug.Log($"  orthographic: {cam.orthographic}");
        Debug.Log($"  orthographicSize: {cam.orthographicSize}");
        Debug.Log($"  nearClipPlane: {cam.nearClipPlane}");
        Debug.Log($"  farClipPlane: {cam.farClipPlane}");
        Debug.Log($"  cullingMask: {cam.cullingMask} (0=Nothing, -1=Everything)");
        Debug.Log($"  clearFlags: {cam.clearFlags}");
        Debug.Log($"  backgroundColor: {cam.backgroundColor}");
        Debug.Log($"  depth: {cam.depth}");
        Debug.Log($"  enabled: {cam.enabled}");
        Debug.Log($"  gameObject.activeInHierarchy: {cam.gameObject.activeInHierarchy}");

        // カメラのビューポート範囲を計算
        float height = cam.orthographicSize * 2;
        float width = height * cam.aspect;
        Vector3 camPos = cam.transform.position;
        Debug.Log($"  表示範囲: X[{camPos.x - width/2:F2} ~ {camPos.x + width/2:F2}], Y[{camPos.y - height/2:F2} ~ {camPos.y + height/2:F2}]");

        // すべてのSpriteRendererがカメラの表示範囲内にあるか確認
        var renderers = FindObjectsOfType<SpriteRenderer>();
        Debug.Log($"[CameraDebugger] === SpriteRenderer位置チェック ({renderers.Length}個) ===");

        foreach (var sr in renderers)
        {
            if (sr.sprite == null) continue;

            Vector3 pos = sr.transform.position;
            bool inViewX = pos.x >= camPos.x - width/2 && pos.x <= camPos.x + width/2;
            bool inViewY = pos.y >= camPos.y - height/2 && pos.y <= camPos.y + height/2;
            bool inViewZ = pos.z >= cam.nearClipPlane && pos.z <= cam.farClipPlane;
            bool inView = inViewX && inViewY && inViewZ;

            // レイヤーがカリングマスクに含まれているか
            int layer = sr.gameObject.layer;
            bool layerVisible = (cam.cullingMask & (1 << layer)) != 0;

            string status = inView && layerVisible ? "OK" : "NG";
            if (!inView) status += "(範囲外)";
            if (!layerVisible) status += "(レイヤー非表示)";

            Debug.Log($"  {sr.gameObject.name}: pos={pos}, layer={layer}({LayerMask.LayerToName(layer)}), visible={status}");
        }
    }
}
