using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// タイトルシーンのコントローラー。
/// メニュー選択とシーン遷移を管理します。
/// </summary>
public class TitleSceneController : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("タイトル表示後、入力を受け付けるまでの待機時間")]
    [SerializeField] private float inputDelay = 1.0f;

    [Header("参照")]
    [SerializeField] private TitleView titleView;

    public enum TitleState
    {
        FadeIn,
        WaitForStart,
        Menu,
        FadeOut
    }

    public enum MenuSelection
    {
        GameStart,
        Options,
        Exit
    }

    private TitleState currentState;
    private MenuSelection currentSelection;
    private float elapsedTime;
    private float fadeProgress;

    // 入力クールダウン
    private float inputCooldown;
    private const float INPUT_COOLDOWN_TIME = 0.15f;

    public TitleState CurrentState => currentState;
    public MenuSelection CurrentSelection => currentSelection;
    public float FadeProgress => fadeProgress;

    private void Start()
    {
        elapsedTime = 0f;
        fadeProgress = 0f;
        currentState = TitleState.FadeIn;
        currentSelection = MenuSelection.GameStart;
        inputCooldown = 0f;

        // GameSceneManager がなければ生成
        if (GameSceneManager.Instance == null)
        {
            var go = new GameObject("GameSceneManager");
            go.AddComponent<GameSceneManager>();
        }

        // TitleViewの参照を自動取得
        if (titleView == null)
        {
            titleView = GetComponent<TitleView>();
        }
    }

    private void Update()
    {
        // クールダウン更新
        if (inputCooldown > 0f)
        {
            inputCooldown -= Time.deltaTime;
        }

        switch (currentState)
        {
            case TitleState.FadeIn:
                UpdateFadeIn();
                break;
            case TitleState.WaitForStart:
                UpdateWaitForStart();
                break;
            case TitleState.Menu:
                UpdateMenu();
                break;
            case TitleState.FadeOut:
                UpdateFadeOut();
                break;
        }
    }

    private void UpdateFadeIn()
    {
        elapsedTime += Time.deltaTime;
        fadeProgress = Mathf.Clamp01(elapsedTime / inputDelay);

        if (elapsedTime >= inputDelay)
        {
            currentState = TitleState.WaitForStart;
            fadeProgress = 1f;
        }
    }

    private void UpdateWaitForStart()
    {
        // 任意のキー/ボタンでメニューへ
        if (AnyInputPressed())
        {
            currentState = TitleState.Menu;
            inputCooldown = INPUT_COOLDOWN_TIME;
        }
    }

    private void UpdateMenu()
    {
        if (inputCooldown > 0f) return;

        // 上下でメニュー選択
        float vertical = GetVerticalInput();
        if (vertical > 0.5f)
        {
            SelectPrevious();
            inputCooldown = INPUT_COOLDOWN_TIME;
        }
        else if (vertical < -0.5f)
        {
            SelectNext();
            inputCooldown = INPUT_COOLDOWN_TIME;
        }

        // 決定
        if (ConfirmPressed())
        {
            ExecuteSelection();
            inputCooldown = INPUT_COOLDOWN_TIME;
        }

        // キャンセルで戻る
        if (CancelPressed())
        {
            currentState = TitleState.WaitForStart;
            inputCooldown = INPUT_COOLDOWN_TIME;
        }
    }

    private void UpdateFadeOut()
    {
        elapsedTime += Time.deltaTime;
        fadeProgress = 1f - Mathf.Clamp01(elapsedTime / 0.5f);

        if (elapsedTime >= 0.5f)
        {
            // 選択に応じて遷移
            switch (currentSelection)
            {
                case MenuSelection.GameStart:
                    GameSceneManager.Instance.GoToWorldMap();
                    break;
                case MenuSelection.Exit:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }
    }

    private void SelectNext()
    {
        int count = System.Enum.GetValues(typeof(MenuSelection)).Length;
        currentSelection = (MenuSelection)(((int)currentSelection + 1) % count);
    }

    private void SelectPrevious()
    {
        int count = System.Enum.GetValues(typeof(MenuSelection)).Length;
        currentSelection = (MenuSelection)(((int)currentSelection - 1 + count) % count);
    }

    private void ExecuteSelection()
    {
        switch (currentSelection)
        {
            case MenuSelection.GameStart:
                currentState = TitleState.FadeOut;
                elapsedTime = 0f;
                break;
            case MenuSelection.Options:
                // TODO: オプション画面（将来的に実装）
                Debug.Log("Options - Not implemented yet");
                break;
            case MenuSelection.Exit:
                currentState = TitleState.FadeOut;
                elapsedTime = 0f;
                break;
        }
    }

    #region Input Helpers

    private bool AnyInputPressed()
    {
        // キーボード
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonEast.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    private float GetVerticalInput()
    {
        float value = 0f;

        // キーボード
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
            {
                value = 1f;
            }
            else if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
            {
                value = -1f;
            }
        }

        // ゲームパッド（左スティックまたはDパッド）
        if (Gamepad.current != null)
        {
            float stick = Gamepad.current.leftStick.y.ReadValue();
            float dpad = Gamepad.current.dpad.y.ReadValue();

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
