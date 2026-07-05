using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ワールドマップシーン専用のコントローラー。
/// WorldMapPhase の機能を独立シーンとして動作させます。
/// キーボード/ゲームパッド両対応。
/// </summary>
public class WorldMapSceneController : MonoBehaviour
{
    [Header("マップデータ")]
    [Tooltip("表示するワールドマップデータ")]
    [SerializeField] private WorldMapData worldMap;

    [Header("ゲーム進行")]
    [Tooltip("ゲーム進行状態。シーン間で共有される")]
    [SerializeField] private RunProgress runProgress;

    private WorldMapView view;

    // 入力クールダウン
    private float inputCooldown;
    private const float INPUT_COOLDOWN_TIME = 0.15f;

    private void Start()
    {
        // GameSceneManager がなければ生成
        if (GameSceneManager.Instance == null)
        {
            var go = new GameObject("GameSceneManager");
            go.AddComponent<GameSceneManager>();
        }

        // RunProgress がなければシーン内から探すか生成
        if (runProgress == null)
        {
            runProgress = FindObjectOfType<RunProgress>();
        }

        var map = (runProgress != null && runProgress.worldMap != null) ? runProgress.worldMap : worldMap;
        if (map == null)
        {
            Debug.LogWarning("[WorldMapSceneController] WorldMapData が未設定です。");
            return;
        }

        SpawnView(map);
    }

    private void Update()
    {
        // クールダウン更新
        if (inputCooldown > 0f)
        {
            inputCooldown -= Time.deltaTime;
        }

        if (view == null || view.IsLocked) return;

        // 移動入力
        if (inputCooldown <= 0f)
        {
            float horizontal = GetHorizontalInput();
            if (horizontal < -0.5f)
            {
                view.MoveCursor(-1);
                inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (horizontal > 0.5f)
            {
                view.MoveCursor(+1);
                inputCooldown = INPUT_COOLDOWN_TIME;
            }
        }

        // 決定
        if (ConfirmPressed())
        {
            view.Confirm();
        }

        // キャンセル（タイトルへ戻る）
        if (CancelPressed())
        {
            GameSceneManager.Instance.GoToTitle();
        }
    }

    private void SpawnView(WorldMapData map)
    {
        if (view != null)
        {
            view.StageSelected -= OnStageSelected;
            Destroy(view.gameObject);
        }

        var go = new GameObject("WorldMapView");
        view = go.AddComponent<WorldMapView>();
        view.StageSelected += OnStageSelected;
        view.Build(map, runProgress);
    }

    private void OnStageSelected(StageData stage)
    {
        Debug.Log($"[WorldMapSceneController] ステージ選択: {stage.displayName}");

        // RunProgress に記録
        if (runProgress != null)
        {
            runProgress.BeginStage(stage);
        }

        // 選択されたステージをGameSceneManagerに保存
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.selectedStage = stage;
        }

        // キャラクター選択画面へ遷移
        GameSceneManager.Instance.GoToCharacterSelect();
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            view.StageSelected -= OnStageSelected;
        }
    }

    #region Input Helpers

    private float GetHorizontalInput()
    {
        float value = 0f;

        // キーボード
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
            {
                value = -1f;
            }
            else if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
            {
                value = 1f;
            }
        }

        // ゲームパッド（左スティックまたはDパッド）
        if (Gamepad.current != null)
        {
            float stick = Gamepad.current.leftStick.x.ReadValue();
            float dpad = Gamepad.current.dpad.x.ReadValue();

            if (Mathf.Abs(stick) > Mathf.Abs(value))
            {
                value = stick;
            }
            if (Mathf.Abs(dpad) > Mathf.Abs(value))
            {
                value = dpad;
            }
        }

        return value;
    }

    private bool ConfirmPressed()
    {
        // キーボード: Enter/Space/Z
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.zKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        // ゲームパッド: A/Cross
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool CancelPressed()
    {
        // キーボード: Escape/X
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Keyboard.current.xKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        // ゲームパッド: B/Circle
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    #endregion
}
