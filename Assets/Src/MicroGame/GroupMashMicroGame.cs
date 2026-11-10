using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// グループ型アクションコマンド「れんだ！」。
/// 手番のプレイヤーが SPACE 連打で自分の領域を満たす。満たしたら攻撃命中。時間切れは不発。
/// 共通の進行(領域分割・交代の光・タイマー)は <see cref="GroupRelayMicroGame"/>。
/// </summary>
public class GroupMashMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "れんだ！";
    protected override string PromptText => "SPACE!";
    // 連打は塗りで成否が見て分かるため、HIT/MISS ラベルは出さない(従来どおり)
    protected override bool ShowResultLabels => false;

    private RectTransform[] fillRt;
    private float[] fill;
    private float gainPerPress;
    private float decayPerSec;

    protected override float ComputeTurnLimit(MicroGameContext c) => Mathf.Max(4f, c.timeLimit);

    protected override void OnSetup(MicroGameContext c)
    {
        // 難易度: かなり易しめ(仮)。1押しで大きく増え、減衰は小さい。
        gainPerPress = Mathf.Max(0.12f, 0.17f / Mathf.Max(0.7f, c.speedMul));
        decayPerSec = 0.08f + 0.05f * c.speedMul;
        fill = new float[PlayerCount];
        fillRt = new RectTransform[PlayerCount];
    }

    protected override void BuildZone(int i, RectTransform zone, Color col)
    {
        fillRt[i] = CreateLeftFill(zone, $"Fill{i}", col, ZoneInnerH - 4f);
    }

    protected override TurnResult TickTurn(int i, float dt)
    {
        fill[i] -= decayPerSec * dt;
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) fill[i] += gainPerPress;
        fill[i] = Mathf.Clamp01(fill[i]);

        if (fill[i] >= 1f) fill[i] = 1f;
        UpdateFillVisual(i);
        return fill[i] >= 1f ? TurnResult.Hit : TurnResult.Continue;
    }

    private void UpdateFillVisual(int i)
    {
        if (fillRt == null || i < 0 || i >= fillRt.Length || fillRt[i] == null) return;
        float w = (ZoneW - 6f) * fill[i];
        fillRt[i].sizeDelta = new Vector2(w, fillRt[i].sizeDelta.y);
    }
}
