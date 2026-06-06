/// <summary>
/// 戦闘の各フェーズが実装するライフサイクルインターフェース。
/// BattleStateMachine がフェーズを制御するために Enter/Exit/Tick を呼び出します。
/// </summary>
public interface IBattlePhase
{
    /// <summary>
    /// フェーズに入るときに一度呼ばれる。UI の表示や初期化を行う。
    /// </summary>
    void Enter();

    /// <summary>
    /// フェーズを抜けるときに一度呼ばれる。登録解除や後片付けを行う。
    /// </summary>
    void Exit();

    /// <summary>
    /// 毎フレーム呼ばれる更新処理。MonoBehaviour.Update と同様の用途で使う。
    /// </summary>
    void Tick();

    /// <summary>
    /// true の間、BattleStateMachine のデバッグ Enter による自動遷移を抑止する。
    /// フェーズが Enter キーを自前で消費する/独自に遷移を管理する場合に true。
    /// </summary>
    bool BlocksDebugAdvance { get; }
}
