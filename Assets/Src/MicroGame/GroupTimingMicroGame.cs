using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// アクションコマンド「タイミング！」(ちぢむリング)。
/// 大きなリングが中央の的(丸)に向かってちぢんでくる。リングが的の縁に重なった瞬間に SPACE で命中。
/// 押せるのは手番につき1回。早すぎ/遅すぎ(リングが的を通り過ぎる)/時間切れ は不発。
/// 難易度: speedMul=ちぢむ速さ / rangeScale=許容のずれ幅 / interference=リングが点滅して見えにくい。
/// </summary>
public class GroupTimingMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "タイミング！";
    protected override string PromptText => "リングが まとに かさなったら SPACE！";

    private const float TargetD = 120f;   // 的の直径
    private const float StartD = 270f;    // リングの出現時の直径

    private Image targetFill;
    private Image targetRing;
    private Image toleranceOuter;
    private Image toleranceInner;
    private Image approach;

    private float shrinkPerSec;   // 直径の縮小速度(px/秒)
    private float tolerance;      // 許容する直径のずれ(px)
    private float interference;
    private System.Random rng;

    private float delay;          // リングが出るまでの待ち(毎手番ランダム。読まれないように)
    private float d;
    private bool pressed;

    protected override void OnSetup(MicroGameContext c)
    {
        shrinkPerSec = 120f * Mathf.Max(0.8f, c.speedMul); // 下限0.8: 最遅でも(出現待ち0.6秒込み)約2.2秒で的に届き、制限時間(3秒以上)に収まる
        tolerance = Mathf.Clamp(24f * c.rangeScale / Mathf.Max(1f, 0.8f + 0.2f * c.speedMul), 10f, 40f);
        interference = Mathf.Clamp01(c.interference);
        rng = c.rng ?? new System.Random();
    }

    protected override void BuildPlay(RectTransform play)
    {
        targetFill = Shape(play, "TargetFill", AcShapes.Circle(), Color.white, Vector2.zero, new Vector2(TargetD, TargetD));
        toleranceOuter = Shape(play, "TolOuter", AcShapes.Ring(), Color.white, Vector2.zero, Vector2.zero);
        toleranceInner = Shape(play, "TolInner", AcShapes.Ring(), Color.white, Vector2.zero, Vector2.zero);
        targetRing = Shape(play, "TargetRing", AcShapes.Ring(), Color.white, Vector2.zero, new Vector2(TargetD, TargetD));
        approach = Shape(play, "Approach", AcShapes.Ring(), Color.white, Vector2.zero, new Vector2(StartD, StartD));
    }

    protected override void OnTurnStart(int i, Color col)
    {
        delay = 0.15f + 0.45f * (float)rng.NextDouble();
        d = StartD;
        pressed = false;

        targetFill.color = WithAlpha(col, 0.35f);
        targetRing.color = col;
        // 許容範囲を薄いリング2本で示す
        toleranceOuter.rectTransform.sizeDelta = new Vector2(TargetD + tolerance, TargetD + tolerance);
        toleranceInner.rectTransform.sizeDelta = new Vector2(TargetD - tolerance, TargetD - tolerance);
        toleranceOuter.color = WithAlpha(Color.white, 0.25f);
        toleranceInner.color = WithAlpha(Color.white, 0.25f);
        approach.color = WithAlpha(Color.white, 0f); // 出現まで隠す
        approach.rectTransform.sizeDelta = new Vector2(StartD, StartD);
    }

    protected override TurnResult TickTurn(int i, float dt)
    {
        if (pressed) return TurnResult.Continue;

        bool visible = PlayerTime >= delay;
        if (visible)
        {
            d -= shrinkPerSec * dt;
            approach.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, d), Mathf.Max(0f, d));
            float a = 1f;
            if (interference > 0f && Mathf.Sin(PlayerTime * 13f) > 0.3f) a = 1f - 0.85f * interference;
            approach.color = WithAlpha(Color.white, a);

            // 的を通り過ぎた=遅すぎ
            if (d < TargetD - tolerance) return TurnResult.Miss;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            pressed = true;
            // 出現前に押した場合も「早すぎ」で不発
            bool hit = visible && Mathf.Abs(d - TargetD) <= tolerance;
            return hit ? TurnResult.Hit : TurnResult.Miss;
        }
        return TurnResult.Continue;
    }

    protected override void OnTurnEnd(int i, bool hit)
    {
        if (hit)
        {
            targetRing.color = HitColor;
            approach.color = HitColor;
        }
        else
        {
            targetFill.color = WithAlpha(MissColor, 0.25f);
            approach.color = WithAlpha(new Color(1f, 0.35f, 0.35f, 1f), 0.9f);
        }
    }
}
