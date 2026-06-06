using System;
using UnityEngine;

/// <summary>
/// 1回分のミニゲームに渡る難易度・環境パラメータ。
/// ramp(ラウンド難化)とカード効果ノブを合成した実効値が入る。
/// </summary>
public class MicroGameContext
{
    public RectTransform uiRoot;     // ミニゲームが UI を生成する親(画面中央想定)
    public System.Random rng;
    public int roundIndex;           // 0 始まり。大きいほど難しい
    public int playerIndex;          // 実行中プレイヤー(ホットシート式の旧設計用。グループ式では未使用)

    [Tooltip("グループ式ミニゲームでの参加プレイヤー数。1ならホットシート扱い。")]
    public int playerCount = 1;

    // 実効ノブ(ActionCommandModifiers が算出)
    public float timeLimit = 4f;     // 制限時間(秒)
    public float speedMul = 1f;      // 進行/対象速度
    public int buttonCount = 2;      // 入力ボタン数(順番入力等)
    public float rangeScale = 1f;    // 当たり/許容範囲の広さ(小さいほど難)
    public float interference = 0f;  // 視覚妨害 0..1

    public Color accent = Color.white;       // 単色アクセント(ホットシート用カテゴリ色等)
    public Color[] playerColors;             // 各プレイヤー色(グループ式 UI 用、playerCount と同じ並び)

    /// <summary>
    /// グループ式ミニゲームで「指定プレイヤーの攻撃が命中した瞬間」に呼ばれるコールバック。
    /// ActionCommandPhase が登録し、即座にエンタメ演出 + 部位ダメージを適用する。
    /// </summary>
    public Action<int> OnPlayerAttack;
}

/// <summary>
/// WarioWare 的に即終わる小ミニゲームの共通インターフェース。
/// 実装を増やすだけで MicroGameRegistry 経由でランダム選出される。
/// ライフサイクル: Begin → 毎フレーム Tick(true で終了) → Succeeded 参照 → Cleanup。
/// </summary>
public interface IMicroGame
{
    /// <summary>MiniGameStart に表示する名前。</summary>
    string DisplayName { get; }

    /// <summary>UI 構築・初期化。</summary>
    void Begin(MicroGameContext ctx);

    /// <summary>毎フレーム更新。true を返したら終了。</summary>
    bool Tick(float deltaTime);

    /// <summary>終了時の成否(Tick が true を返した後に参照)。</summary>
    bool Succeeded { get; }

    /// <summary>生成 UI の破棄など後始末。</summary>
    void Cleanup();
}
