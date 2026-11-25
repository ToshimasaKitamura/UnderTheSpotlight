/// <summary>
/// デバッグ室(DebugRoom)からバトルを「どのサブシステムだけ」で起動するかを運ぶ静的ホルダ。
/// シーンを跨いで有効(static)。BattleStateMachine がこれを見て開始フェーズを決め、
/// 指定モードではそのフェーズをループ(他へ遷移させない)して単体チェックしやすくする。
/// </summary>
public static class DebugLaunch
{
    public enum Mode
    {
        None,          // 通常プレイ(タイトル→…→バトル)
        Danmaku,       // 弾幕(回避)だけ
        ActionCommand, // アクションコマンドだけ
        Character,     // キャラ登場・挙動だけ(BossIntro)
    }

    /// <summary>現在のデバッグ起動モード。None なら通常。</summary>
    public static Mode Current = Mode.None;

    /// <summary>
    /// 弾種・パターン検証用に、ステージ/ボスに紐づかない EnemyData を実行時注入する差し替え。
    /// null なら通常どおり選択ステージの該当ボスを使う。BattleSceneController がこれを優先する。
    /// </summary>
    public static EnemyData OverrideEnemy;

    /// <summary>Danmaku / ActionCommand は単体チェック用にそのフェーズをループさせる。</summary>
    public static bool LoopsPhase => Current == Mode.Danmaku || Current == Mode.ActionCommand;

    public static void Clear()
    {
        Current = Mode.None;
        OverrideEnemy = null;
    }

    // デバッグ室が実行時に作った EnemyData(HideAndDontSave なので自動では回収されない)
    private static EnemyData runtimeEnemy;

    /// <summary>デバッグ室で実行時に作った EnemyData を差し込む。前に作ったものは破棄する。</summary>
    public static void SetRuntimeOverride(EnemyData e)
    {
        ReleaseRuntimeEnemy();
        runtimeEnemy = e;
        OverrideEnemy = e;
    }

    /// <summary>
    /// 実行時に作った EnemyData を破棄して Clear する。バトルが動いていないとき(デバッグ室から起動する直前)に呼ぶ。
    /// バトル中(Esc でフェード中など)に破棄すると、まだ参照しているバトルが壊れるため Clear と分けている。
    /// </summary>
    public static void ResetForNewLaunch()
    {
        ReleaseRuntimeEnemy();
        Clear();
    }

    private static void ReleaseRuntimeEnemy()
    {
        if (runtimeEnemy != null) UnityEngine.Object.Destroy(runtimeEnemy);
        runtimeEnemy = null;
    }
}
