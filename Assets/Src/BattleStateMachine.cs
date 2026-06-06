using UnityEngine;
// 新しい Input System を利用するための名前空間。パッケージが有効な場合に使用します。
using UnityEngine.InputSystem;

/// <summary>
/// 戦闘の簡易ステートマシン。
/// 各フェーズは IBattlePhase を実装し、Enter/Exit/Tick が呼ばれます。
/// </summary>
public class BattleStateMachine : MonoBehaviour
{
    /// <summary>
    /// 利用するステートの列挙。
    /// 必要に応じてここに状態を追加してください。
    /// </summary>
    public enum State
    {
        WorldMap,
        CardSelect,
        PartSelect,
        ActionCommand,
        Dodge,
    }

    [Header("フェーズコンポーネント(Inspectorで割り当て、未割当の場合は Awake で探索されます)")]
    [SerializeField] private WorldMapPhase worldMapPhase;
    [SerializeField] private CardSelectPhase cardSelectPhase;
    [SerializeField] private PartSelectPhase partSelectPhase;
    [SerializeField] private ActionCommandPhase actionCommandPhase;
    [SerializeField] private DodgePhase dodgePhase;

    [Header("起動")]
    [Tooltip("Start で最初に入る状態。マップを上位にする場合は WorldMap、戦闘から始めたいなら CardSelect。")]
    public State initialState = State.WorldMap;
    [Tooltip("GameFont の初期化に使う GameTuning。displayFont / bodyFont があれば全 UI に反映。")]
    [SerializeField] private GameTuning fontTuning;

    // 現在アクティブなフェーズ実装への参照
    private IBattlePhase currentPhase;
    // 現在の列挙値としての状態
    private State currentState;

    private void Awake()
    {
        // フォント: GameTuning.displayFont / bodyFont を全 UI に反映(任意)
        if (fontTuning == null)
        {
            // 自動探索: シーン内の GameTuning ScriptableObject を Inspector から拾えなければ、
            // CardSelectPhase 等が持ってる tuning を試す
            var anyTuning = Resources.FindObjectsOfTypeAll<GameTuning>();
            if (anyTuning != null && anyTuning.Length > 0) fontTuning = anyTuning[0];
        }
        if (fontTuning != null) GameFont.RegisterFromTuning(fontTuning);

        // Inspector にセットされていなければ子オブジェクトから自動取得する
        if (worldMapPhase == null) worldMapPhase = GetComponentInChildren<WorldMapPhase>();
        if (cardSelectPhase == null) cardSelectPhase = GetComponentInChildren<CardSelectPhase>();
        if (partSelectPhase == null) partSelectPhase = GetComponentInChildren<PartSelectPhase>();
        if (actionCommandPhase == null) actionCommandPhase = GetComponentInChildren<ActionCommandPhase>();
        if (dodgePhase == null) dodgePhase = GetComponentInChildren<DodgePhase>();
    }

    private void Start()
    {
        // すべてのフェーズをいったん Exit 状態にしてから開始する
        worldMapPhase?.Exit();
        cardSelectPhase?.Exit();
        partSelectPhase?.Exit();
        actionCommandPhase?.Exit();
        dodgePhase?.Exit();

        // 初期状態(WorldMap が無ければ CardSelect で開始)。
        State entry = initialState;
        if (entry == State.WorldMap && worldMapPhase == null) entry = State.CardSelect;
        ChangeState(entry);
    }

    private void Update()
    {
        // フェーズ Tick 中に ChangeState されたかを検知するため遷移前の状態を保持
        State prevState = currentState;

        // 毎フレーム現在フェーズの Tick を呼ぶ
        currentPhase?.Tick();

        // Tick 内でフェーズ遷移が起きていれば、デバッグ Enter の二重発火を防ぐためここで打ち切る
        if (currentState != prevState) return;

        // フェーズが Enter を自前で消費する場合(例: カード選択中)はデバッグ自動遷移しない
        if (currentPhase != null && currentPhase.BlocksDebugAdvance) return;

        // デバッグ用: Enter キーで次のフェーズへ切り替える（テンポラリ）
        // 実装後は入力管理に移すことを推奨
        bool enterPressed = false;

        // 1) 新しい Input System パッケージ経由でのチェック（プロジェクトが Input System を使っている場合）
        // Keyboard.current が null でないことを確認してから参照する
        if (Keyboard.current != null)
        {
            enterPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        }

        // 2) 旧来の Input.System (UnityEngine.Input) をフォールバックで試す。
        // Player Settings が新しい Input System に切り替わっていると例外が投げられるため try/catch で保護する。
        if (!enterPressed)
        {
            try
            {
                enterPressed = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter);
            }
            catch (System.InvalidOperationException)
            {
                // Legacy Input が無効な場合は何もしない（enterPressed は false のまま）
            }
        }

        if (enterPressed)
        {
            NextState();
        }
    }

    /// <summary>
    /// ステート遷移を行う。
    /// 同じステートへの遷移は無視されます。
    /// </summary>
    public void ChangeState(State newState)
    {
        if (currentPhase != null && newState == currentState) return;

        // 現在のフェーズを抜ける
        currentPhase?.Exit();

        currentState = newState;

        // 新しい状態に対応するフェーズへ差し替え
        switch (newState)
        {
            case State.WorldMap: currentPhase = worldMapPhase; break;
            case State.CardSelect: currentPhase = cardSelectPhase; break;
            case State.PartSelect: currentPhase = partSelectPhase; break;
            case State.ActionCommand: currentPhase = actionCommandPhase; break;
            case State.Dodge: currentPhase = dodgePhase; break;
        }

        // 新しいフェーズへ入る
        currentPhase?.Enter();

        // デバッグ: 現在のステートをログに出す
        UnityEngine.Debug.Log($"[BattleStateMachine] 状態遷移 -> {currentState}");
    }

    /// <summary>
    /// 現在の状態から次の状態へ循環的に遷移するユーティリティ。
    /// デバッグやキー入力での切替に使いやすい。
    /// </summary>
    public void NextState()
    {
        // 列挙型を int に変換してインクリメント、範囲をラップする
        int next = ((int)currentState + 1) % System.Enum.GetValues(typeof(State)).Length;
        ChangeState((State)next);
    }
}
