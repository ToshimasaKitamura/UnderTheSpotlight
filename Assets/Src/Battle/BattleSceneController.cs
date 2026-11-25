using UnityEngine;

/// <summary>
/// バトルシーン専用のコントローラー。
/// GameSceneManager から渡されたステージデータを元にバトルを開始します。
/// BattleStateMachine と連携して動作します。
/// </summary>
public class BattleSceneController : MonoBehaviour
{
    [Header("依存コンポーネント")]
    [SerializeField] private BattleStateMachine stateMachine;
    [SerializeField] private EnemyRuntimeState enemyState;
    [SerializeField] private RunProgress runProgress;

    /// <summary>
    /// バトル終了時に呼び出されるイベント
    /// </summary>
    public event System.Action<bool> BattleEnded;

    private void Awake()
    {
        if (stateMachine == null) stateMachine = FindObjectOfType<BattleStateMachine>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (runProgress == null) runProgress = FindObjectOfType<RunProgress>();

        // バトルステートマシンを CardSelect から開始（Start()より前に設定）
        if (stateMachine != null)
        {
            stateMachine.initialState = BattleStateMachine.State.CardSelect;
        }
    }

    private void Start()
    {
        // GameSceneManager からステージデータを取得
        if (GameSceneManager.Instance != null)
        {
            var stage = GameSceneManager.Instance.selectedStage;
            var bossIdx = GameSceneManager.Instance.bossIndex;

            // デバッグ: ステージ/ボス未割当の弾種・パターン検証用 EnemyData を優先
            EnemyData chosen = DebugLaunch.OverrideEnemy;
            if (chosen == null && stage != null && stage.bosses != null && stage.bosses.Count > bossIdx)
                chosen = stage.bosses[bossIdx];

            // EnemyRuntimeState にボスデータを設定
            if (enemyState != null && chosen != null)
            {
                enemyState.enemyData = chosen;
                enemyState.ResetAll();
            }

            // RunProgress に記録(ステージがある場合のみ)。実際に戦うボスの番号から始める。
            // 上書き(デバッグの弾種検証)時は連戦ランプを乗せない=0 番から。
            if (runProgress != null && stage != null)
            {
                runProgress.BeginStage(stage, DebugLaunch.OverrideEnemy != null ? 0 : bossIdx);
            }

            string label = chosen != null ? chosen.displayName : "(none)";
            Debug.Log($"[BattleSceneController] バトル開始: boss={label}, override={(DebugLaunch.OverrideEnemy != null)}, stage={(stage != null ? stage.displayName : "null")}");
        }
    }

    /// <summary>
    /// バトル勝利時に呼び出す
    /// </summary>
    public void OnBattleVictory()
    {
        Debug.Log("[BattleSceneController] 勝利!");
        BattleEnded?.Invoke(true);
        GameSceneManager.Instance?.GoToResult(true);
    }

    /// <summary>
    /// バトル敗北時に呼び出す
    /// </summary>
    public void OnBattleDefeat()
    {
        Debug.Log("[BattleSceneController] 敗北...");
        BattleEnded?.Invoke(false);
        GameSceneManager.Instance?.GoToResult(false);
    }

    /// <summary>
    /// バトルを中断してワールドマップに戻る
    /// </summary>
    public void ReturnToWorldMap()
    {
        GameSceneManager.Instance?.GoToWorldMap();
    }
}
