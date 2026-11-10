using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// アクションコマンド「ためて はなせ！」(三角のパワー)。
/// SPACE を長押しすると大きな三角が下から色で満ちていく。離した時の高さが白い帯の中なら命中。
/// 帯の手前で離す/てっぺんまで溜めて破裂/時間切れ は不発。
/// 前の手番から押しっぱなしでも溜まらないよう、プレイ中に新しく押した時から溜め始める。
/// 難易度: speedMul=溜まる速さ / rangeScale=帯の広さ。
/// </summary>
public class GroupHoldMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "ためて はなせ！";
    protected override string PromptText => "SPACE ながおしで ためて おびで はなせ！";

    private const float TriW = 250f;
    private const float TriH = 230f;
    private const float TriY = -6f;        // プレイエリア内での三角の中心 y

    private Image triBack;
    private Image triFill;
    private Image band;
    private Image bandTop;
    private Image bandBottom;

    private float chargePerSec;
    private float bandHalf;
    private System.Random rng;

    private float bandCenter;   // 0..1(三角の高さに対する割合)
    private float charge;
    private bool charging;

    protected override void OnSetup(MicroGameContext c)
    {
        // 下限0.8: 最遅でも帯の上端まで約2.1秒で届き、制限時間(3秒以上)に収まる
        chargePerSec = 0.55f * Mathf.Max(0.8f, c.speedMul);
        bandHalf = Mathf.Clamp(0.08f * c.rangeScale, 0.035f, 0.16f);
        rng = c.rng ?? new System.Random();
    }

    protected override void BuildPlay(RectTransform play)
    {
        triBack = Shape(play, "TriBack", AcShapes.Triangle(), Color.white, new Vector2(0f, TriY), new Vector2(TriW, TriH));
        triFill = Shape(play, "TriFill", AcShapes.Triangle(), Color.white, new Vector2(0f, TriY), new Vector2(TriW, TriH));
        triFill.type = Image.Type.Filled;
        triFill.fillMethod = Image.FillMethod.Vertical;
        triFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        triFill.fillAmount = 0f;

        band = AcUi.Image(play, "Band", WithAlpha(Color.white, 0.22f));
        bandTop = AcUi.Image(play, "BandTop", Color.white);
        bandBottom = AcUi.Image(play, "BandBottom", Color.white);
    }

    protected override void OnTurnStart(int i, Color col)
    {
        charge = 0f;
        charging = false;
        // 帯は 55%〜92% の間。てっぺん(破裂)と被らないよう上端に余白を取る
        bandCenter = Mathf.Lerp(0.55f + bandHalf, 0.92f - bandHalf, (float)rng.NextDouble());

        triBack.color = Dim(col, 0.28f);
        triFill.color = col;
        triFill.fillAmount = 0f;

        float lineW = TriW + 60f;
        float yLo = HeightToY(bandCenter - bandHalf);
        float yHi = HeightToY(bandCenter + bandHalf);
        PlaceCenter(band.rectTransform, new Vector2(0f, (yLo + yHi) * 0.5f), new Vector2(lineW, yHi - yLo));
        PlaceCenter(bandTop.rectTransform, new Vector2(0f, yHi), new Vector2(lineW, 4f));
        PlaceCenter(bandBottom.rectTransform, new Vector2(0f, yLo), new Vector2(lineW, 4f));
        band.color = WithAlpha(Color.white, 0.22f);
        bandTop.color = Color.white;
        bandBottom.color = Color.white;
    }

    protected override TurnResult TickTurn(int i, float dt)
    {
        var kb = Keyboard.current;
        if (kb == null) return TurnResult.Continue;

        if (!charging)
        {
            if (kb.spaceKey.wasPressedThisFrame) charging = true;
            return TurnResult.Continue;
        }

        if (kb.spaceKey.isPressed)
        {
            charge += chargePerSec * dt;
            if (charge >= 1f)
            {
                charge = 1f;           // 溜めすぎ=破裂
                triFill.fillAmount = 1f;
                return TurnResult.Miss;
            }
            triFill.fillAmount = charge;
            return TurnResult.Continue;
        }

        // 離した
        bool hit = Mathf.Abs(charge - bandCenter) <= bandHalf;
        return hit ? TurnResult.Hit : TurnResult.Miss;
    }

    protected override void OnTurnEnd(int i, bool hit)
    {
        if (hit)
        {
            band.color = WithAlpha(HitColor, 0.45f);
            bandTop.color = HitColor;
            bandBottom.color = HitColor;
        }
        else
        {
            triFill.color = Dim(triFill.color, 0.45f);
        }
    }

    // 三角の高さの割合(0=底辺, 1=頂点) → プレイエリア内の y
    private static float HeightToY(float h) => TriY - TriH / 2f + TriH * h;
}
