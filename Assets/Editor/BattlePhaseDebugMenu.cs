using UnityEngine;
using UnityEditor;

/// <summary>
/// Unity メニューバー「Debug/フェーズ移動」から、各フェーズの初め/終わりへジャンプする
/// デバッグ用メニュー。Play 中にシーンの BattleStateMachine を探して ChangeState する。
///   初め  = そのフェーズの開始へ
///   終わり = そのフェーズ終了直後の状態(= 次フェーズの開始)へ
/// </summary>
public static class BattlePhaseDebugMenu
{
    private const string Root = "Debug/フェーズ移動/";

    private static void Jump(BattleStateMachine.State s)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[フェーズ移動] Play モード中に実行してください。");
            return;
        }
        var sm = Object.FindObjectOfType<BattleStateMachine>();
        if (sm == null)
        {
            Debug.LogWarning("[フェーズ移動] シーンに BattleStateMachine が見つかりません。");
            return;
        }
        sm.ChangeState(s);
        Debug.Log($"[フェーズ移動] → {s}");
    }

    /// <summary>「終わり」= そのフェーズ終了直後の状態(次フェーズの開始)。</summary>
    private static BattleStateMachine.State NextFlow(BattleStateMachine.State s)
    {
        switch (s)
        {
            case BattleStateMachine.State.BossIntro: return BattleStateMachine.State.CardSelect;
            case BattleStateMachine.State.CardSelect: return BattleStateMachine.State.PartSelect;
            case BattleStateMachine.State.PartSelect: return BattleStateMachine.State.ActionCommand;
            case BattleStateMachine.State.ActionCommand: return BattleStateMachine.State.BattleResult;
            case BattleStateMachine.State.BattleResult: return BattleStateMachine.State.Dodge;
            case BattleStateMachine.State.Dodge: return BattleStateMachine.State.CardSelect;
            default: return s;
        }
    }

    // ===== 1. ボス登場 =====
    [MenuItem(Root + "1. ボス登場/初め へ", false, 0)]
    private static void BossIntroStart() => Jump(BattleStateMachine.State.BossIntro);
    [MenuItem(Root + "1. ボス登場/終わり へ", false, 1)]
    private static void BossIntroEnd() => Jump(NextFlow(BattleStateMachine.State.BossIntro));

    // ===== 2. カード選択 =====
    [MenuItem(Root + "2. カード選択/初め へ", false, 20)]
    private static void CardSelectStart() => Jump(BattleStateMachine.State.CardSelect);
    [MenuItem(Root + "2. カード選択/終わり へ", false, 21)]
    private static void CardSelectEnd() => Jump(NextFlow(BattleStateMachine.State.CardSelect));

    // ===== 3. 部位選択 =====
    [MenuItem(Root + "3. 部位選択/初め へ", false, 40)]
    private static void PartSelectStart() => Jump(BattleStateMachine.State.PartSelect);
    [MenuItem(Root + "3. 部位選択/終わり へ", false, 41)]
    private static void PartSelectEnd() => Jump(NextFlow(BattleStateMachine.State.PartSelect));

    // ===== 4. アクションコマンド =====
    [MenuItem(Root + "4. アクションコマンド/初め へ", false, 60)]
    private static void ActionCommandStart() => Jump(BattleStateMachine.State.ActionCommand);
    [MenuItem(Root + "4. アクションコマンド/終わり へ", false, 61)]
    private static void ActionCommandEnd() => Jump(NextFlow(BattleStateMachine.State.ActionCommand));

    // ===== 5. 結果表示 =====
    [MenuItem(Root + "5. 結果表示/初め へ", false, 80)]
    private static void BattleResultStart() => Jump(BattleStateMachine.State.BattleResult);
    [MenuItem(Root + "5. 結果表示/終わり へ", false, 81)]
    private static void BattleResultEnd() => Jump(NextFlow(BattleStateMachine.State.BattleResult));

    // ===== 6. 弾幕回避 =====
    [MenuItem(Root + "6. 弾幕回避/初め へ", false, 100)]
    private static void DodgeStart() => Jump(BattleStateMachine.State.Dodge);
    [MenuItem(Root + "6. 弾幕回避/終わり へ", false, 101)]
    private static void DodgeEnd() => Jump(NextFlow(BattleStateMachine.State.Dodge));

    // ===== 便利: 次のフェーズへ / 現在フェーズをログ =====
    [MenuItem(Root + "次のフェーズへ", false, 200)]
    private static void NextPhase()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[フェーズ移動] Play モード中に実行してください。"); return; }
        var sm = Object.FindObjectOfType<BattleStateMachine>();
        if (sm == null) { Debug.LogWarning("[フェーズ移動] BattleStateMachine が見つかりません。"); return; }
        sm.NextState();
        Debug.Log($"[フェーズ移動] 次へ → {sm.CurrentState}");
    }

    [MenuItem(Root + "現在フェーズをログ出力", false, 201)]
    private static void LogCurrent()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[フェーズ移動] Play モード中に実行してください。"); return; }
        var sm = Object.FindObjectOfType<BattleStateMachine>();
        if (sm == null) { Debug.LogWarning("[フェーズ移動] BattleStateMachine が見つかりません。"); return; }
        Debug.Log($"[フェーズ移動] 現在: {sm.CurrentState}");
    }
}
