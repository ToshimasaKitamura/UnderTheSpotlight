using UnityEngine;

/// <summary>
/// 敵(ボス)HPゲージ = ボス直下の共有HPゲージ(BattleFrameUI.sharedHpGauge) を
/// EnemyRuntimeState の部位HP合計に連動させる。
/// アクションコマンド等で部位にダメージが入ると OnPartHpChanged が発火し、ゲージが減る。
/// 連戦で次のボスに替わったら満タンへ。初回ボスは BossIntro の満タン演出に任せる。
/// RuntimeInitializeOnLoadMethod で自動生成・常駐(シーン配置不要)。
/// </summary>
public class EnemyHpGaugeBinder : MonoBehaviour
{
    private static EnemyHpGaugeBinder instance;

    private EnemyRuntimeState state;
    private BattleFrameUI frameUI;
    private EnemyData lastData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[EnemyHpGaugeBinder]");
        instance = go.AddComponent<EnemyHpGaugeBinder>();
        DontDestroyOnLoad(go);
    }

    // 戦闘外では対象が無いので、探すのは 0.5 秒に1回にする(以前は毎フレーム2回シーン全体を検索していた)。
    // BattleFrameUI はバトル開始後に実行時生成されるため、見つかるまでは探し続ける。
    private const float SearchInterval = 0.5f;
    private float nextSearchTime;

    private void Update()
    {
        if ((state == null || frameUI == null) && Time.unscaledTime >= nextSearchTime)
        {
            nextSearchTime = Time.unscaledTime + SearchInterval;
            if (state == null) state = FindObjectOfType<EnemyRuntimeState>();
            if (frameUI == null) frameUI = FindObjectOfType<BattleFrameUI>();
        }
        if (state == null || frameUI == null)
        {
            lastData = null; // 戦闘外 or 未初期化 → 次回入場で初回扱いに戻す
            return;
        }

        if (state.enemyData != lastData)
        {
            bool firstTime = (lastData == null);

            // 二重購読防止のうえで購読(state は同一でも enemyData だけ替わるケースに対応)
            state.OnPartHpChanged -= OnPartHpChanged;
            state.OnPartDestroyed -= OnPartDestroyed;
            state.OnPartHpChanged += OnPartHpChanged;
            state.OnPartDestroyed += OnPartDestroyed;

            lastData = state.enemyData;

            // 連戦の次ボスは即満タン反映。初回ボスは BossIntro の 0→満タン演出に任せる。
            if (!firstTime) UpdateGauge();
        }
    }

    private void OnPartHpChanged(EnemyPart part, int newHp) => UpdateGauge();
    private void OnPartDestroyed(EnemyPart part) => UpdateGauge();

    private void UpdateGauge()
    {
        if (state == null || frameUI == null || state.enemyData == null) return;
        var parts = state.enemyData.parts;
        if (parts == null) return;

        int cur = 0, max = 0;
        foreach (var p in parts)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            max += Mathf.Max(0, state.GetMaxHp(p.id));   // 人数スケール後の最大HPで比率を出す
            cur += state.GetCurrentHp(p.id);
        }
        float ratio = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
        frameUI.SetSharedHpGaugeFill(ratio);
    }
}
