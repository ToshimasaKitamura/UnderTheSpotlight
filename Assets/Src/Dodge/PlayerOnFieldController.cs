using UnityEngine;

/// <summary>
/// 弾幕フィールド上の自機(白い球)の移動制御。
/// 入力は IPlayerOnFieldInput 実装(同 GameObject 上のコンポーネント)から取得し、
/// movementBounds 内に位置をクランプする。入力ソースの差し替えで AI 操作などに対応可能。
/// </summary>
[DisallowMultipleComponent]
public class PlayerOnFieldController : MonoBehaviour
{
    [Tooltip("移動速度(ワールド単位/秒)。")]
    public float moveSpeed = 6f;
    [Tooltip("移動可能範囲。EnableBounds が true のときのみクランプされる。")]
    public Bounds movementBounds;
    [Tooltip("true で movementBounds による位置クランプを有効化。")]
    public bool enableBounds = false;

    private IPlayerOnFieldInput inputSource;

    void Awake()
    {
        inputSource = GetComponent<IPlayerOnFieldInput>();
        if (inputSource == null)
        {
            Debug.LogWarning($"[PlayerOnFieldController] {name} に IPlayerOnFieldInput 実装が見つかりません。入力なしで動作します。");
        }
    }

    void Update()
    {
        if (inputSource == null) return;

        Vector2 input = inputSource.ReadMovement();
        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 newPos = transform.position + (Vector3)input * moveSpeed * Time.deltaTime;

        if (enableBounds)
        {
            newPos.x = Mathf.Clamp(newPos.x, movementBounds.min.x, movementBounds.max.x);
            newPos.y = Mathf.Clamp(newPos.y, movementBounds.min.y, movementBounds.max.y);
        }

        transform.position = newPos;
    }
}
