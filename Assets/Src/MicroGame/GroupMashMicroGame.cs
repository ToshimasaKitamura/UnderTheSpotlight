using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// アクションコマンド「れんだ！」(風船)。
/// SPACE を連打すると中央の丸(風船)がふくらむ。外側のリングの大きさまでふくらませたら命中。
/// 手を止めると少しずつしぼむ。時間切れは不発。
/// 難易度: speedMul が高いほど 1押しでふくらむ量が減り、しぼむ速さが増える。
/// </summary>
public class GroupMashMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "れんだ！";
    protected override string PromptText => "SPACE れんだで ふうせんを ふくらませろ！";

    private const float TargetD = 240f;   // 目標リングの直径
    private const float StartD = 70f;     // 風船の初期直径

    private Image targetRing;
    private Image balloon;
    private Image balloonShine;

    private float fill;          // 0..1
    private float punch;         // 押した瞬間のふくらみ演出 0..1
    private float gainPerPress;
    private float decayPerSec;

    protected override float ComputeTurnLimit(MicroGameContext c) => Mathf.Max(4f, c.timeLimit);

    protected override void OnSetup(MicroGameContext c)
    {
        // 難易度: かなり易しめ(仮)。1押しで大きく増え、減衰は小さい。
        gainPerPress = Mathf.Max(0.07f, 0.11f / Mathf.Max(0.7f, c.speedMul));
        decayPerSec = 0.08f + 0.05f * c.speedMul;
    }

    protected override void BuildPlay(RectTransform play)
    {
        targetRing = Shape(play, "TargetRing", AcShapes.Ring(), Color.white, Vector2.zero, new Vector2(TargetD, TargetD));
        balloon = Shape(play, "Balloon", AcShapes.Circle(), Color.white, Vector2.zero, new Vector2(StartD, StartD));
        balloonShine = Shape(balloon.transform, "Shine", AcShapes.Circle(), new Color(1f, 1f, 1f, 0.45f),
            Vector2.zero, Vector2.zero);
    }

    protected override void OnTurnStart(int i, Color col)
    {
        fill = 0f;
        punch = 0f;
        balloon.color = col;
        targetRing.color = WithAlpha(Color.white, 0.8f);
        UpdateVisual();
    }

    protected override TurnResult TickTurn(int i, float dt)
    {
        fill -= decayPerSec * dt;
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            fill += gainPerPress;
            punch = 1f;
        }
        fill = Mathf.Clamp01(fill);
        punch = Mathf.Max(0f, punch - dt * 8f);
        UpdateVisual();
        return fill >= 1f ? TurnResult.Hit : TurnResult.Continue;
    }

    protected override void OnTurnEnd(int i, bool hit)
    {
        if (hit)
        {
            targetRing.color = HitColor;
        }
        else
        {
            balloon.color = Dim(balloon.color, 0.45f);
        }
    }

    private void UpdateVisual()
    {
        float d = Mathf.Lerp(StartD, TargetD, fill) * (1f + 0.06f * punch);
        balloon.rectTransform.sizeDelta = new Vector2(d, d);
        // ハイライト(左上の小さな丸)で風船らしく
        var srt = balloonShine.rectTransform;
        srt.sizeDelta = new Vector2(d * 0.22f, d * 0.22f);
        srt.anchoredPosition = new Vector2(-d * 0.2f, d * 0.2f);
    }
}
