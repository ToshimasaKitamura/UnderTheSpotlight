using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マップ画面フェーズ。戦闘の上位レイヤーとして BattleStateMachine に組み込む。
/// 役割:
///   - WorldMapView を表示してプレイヤーにステージを選ばせる
///   - 選択された StageData を RunProgress に記録
///   - EnemyRuntimeState の enemyData を選ばれたボスに差し替え
///   - CardSelect に遷移して戦闘ループを起動
/// 戦闘終了時(Dodge → ChangeState(WorldMap)) でここに戻ってくる。
/// </summary>
public class WorldMapPhase : MonoBehaviour, IBattlePhase
{
    [Header("依存(未設定なら自動探索)")]
    [SerializeField] private BattleStateMachine stateMachine;
    [SerializeField] private RunProgress runProgress;
    [SerializeField] private EnemyRuntimeState enemyState;
    [Tooltip("マップ中に非表示にする戦闘 HUD: HP ゲージ。未設定なら自動探索。")]
    [SerializeField] private SharedHpGaugeUI hpGaugeUI;
    [Tooltip("マップ中に非表示にする戦闘 HUD: ボム ゲージ。未設定なら自動探索。")]
    [SerializeField] private BombGaugeUI bombGaugeUI;

    [Header("マップ")]
    [Tooltip("使用する WorldMapData。runProgress.worldMap が未設定の場合のフォールバック。")]
    public WorldMapData worldMap;

    public bool BlocksDebugAdvance => true;

    private WorldMapView view;

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (runProgress == null) runProgress = FindObjectOfType<RunProgress>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (hpGaugeUI == null) hpGaugeUI = FindObjectOfType<SharedHpGaugeUI>();
        if (bombGaugeUI == null) bombGaugeUI = FindObjectOfType<BombGaugeUI>();
    }

    void Start() { enabled = false; }

    public void Enter()
    {
        enabled = true;

        // 戦闘 HUD(HP/ボム)はマップ中は非表示にする
        if (hpGaugeUI != null) hpGaugeUI.SetVisible(false);
        if (bombGaugeUI != null) bombGaugeUI.SetVisible(false);

        // マップに戻ってきた瞬間に、前ステージの状態をクリーンアップ
        if (runProgress != null) runProgress.EndStage();

        var map = (runProgress != null && runProgress.worldMap != null) ? runProgress.worldMap : worldMap;
        if (map == null)
        {
            UnityEngine.Debug.LogWarning("[WorldMapPhase] WorldMapData が未設定です。Inspector で割り当ててください。");
            return;
        }

        SpawnView(map);
    }

    public void Exit()
    {
        enabled = false;
        DestroyView();
        // 戦闘 HUD を戻す(マップから戦闘に入った直後はもう表示状態が必要)
        if (hpGaugeUI != null) hpGaugeUI.SetVisible(true);
        if (bombGaugeUI != null) bombGaugeUI.SetVisible(true);
    }

    public void Tick()
    {
        if (view == null || view.IsLocked) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) view.MoveCursor(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) view.MoveCursor(+1);
        else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) view.MoveCursor(-1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) view.MoveCursor(+1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                 || kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame) view.Confirm();
    }

    private void SpawnView(WorldMapData map)
    {
        DestroyView();
        var go = new GameObject("WorldMapView");
        view = go.AddComponent<WorldMapView>();
        view.StageSelected += OnStageSelected;
        view.Build(map, runProgress);
    }

    private void DestroyView()
    {
        if (view != null)
        {
            view.StageSelected -= OnStageSelected;
            Destroy(view.gameObject);
            view = null;
        }
    }

    private void OnStageSelected(StageData stage)
    {
        UnityEngine.Debug.Log($"[WorldMapPhase] ステージ突入: {stage.displayName} ({stage.id})");
        // RunProgress に突入を通知。連戦の bossIndex は 0 に。
        if (runProgress != null) runProgress.BeginStage(stage);
        // 1体目のボスを EnemyRuntimeState に設定
        if (enemyState != null && stage.bosses != null && stage.bosses.Count > 0)
        {
            enemyState.enemyData = stage.bosses[0];
            enemyState.ResetAll(); // 部位HPを満タンに
        }
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.CardSelect);
    }
}
