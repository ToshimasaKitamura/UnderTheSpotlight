using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// グループ型アクションコマンド「ためて はなせ！」。
/// 手番プレイヤーが SPACE を長押しするとゲージが溜まり、離した瞬間のゲージが「ねらい帯」の中なら命中。
/// 帯の手前で離す/溜めすぎて満タン(破裂)/時間切れ は不発。
/// 前の手番から SPACE を押しっぱなしでも溜まらないよう、手番中に新しく押した時から溜め始める。
/// 難易度: speedMul=溜まる速さ / rangeScale=ねらい帯の広さ。
/// </summary>
public class GroupHoldMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "ためて はなせ！";
    protected override string PromptText => "SPACE ながおしして はなせ！";

    private RectTransform[] fillRt;
    private Image[] fillImg;
    private Image[] bandImg;
    private float[] bandCenter;   // ねらい帯の中心(0..1)
    private float bandHalf;
    private float chargePerSec;

    private float charge;
    private bool charging;

    protected override void OnSetup(MicroGameContext c)
    {
        chargePerSec = 0.55f * Mathf.Max(0.5f, c.speedMul);
        bandHalf = Mathf.Clamp(0.08f * c.rangeScale, 0.035f, 0.16f);

        fillRt = new RectTransform[PlayerCount];
        fillImg = new Image[PlayerCount];
        bandImg = new Image[PlayerCount];
        bandCenter = new float[PlayerCount];
        var rng = c.rng ?? new System.Random();
        for (int i = 0; i < PlayerCount; i++)
        {
            // 帯は 55%〜92% の間。満タン(破裂)と被らないよう上端に余白を取る
            bandCenter[i] = Mathf.Lerp(0.55f + bandHalf, 0.92f - bandHalf, (float)rng.NextDouble());
        }
    }

    protected override void BuildZone(int i, RectTransform zone, Color col)
    {
        float w = ZoneInnerW;
        float h = ZoneInnerH;

        fillRt[i] = CreateLeftFill(zone, $"Fill{i}", col, h - 4f);
        fillImg[i] = fillRt[i].GetComponent<Image>();

        // ねらい帯(白い半透明の帯＋両端の線)。塗りより手前に描く
        var band = AcUi.Image(zone, $"Band{i}", new Color(1f, 1f, 1f, 0.28f));
        PlaceInZone(band.rectTransform, bandCenter[i] * w, 0f, bandHalf * 2f * w, h);
        bandImg[i] = band;
        var l = AcUi.Image(zone, $"BandL{i}", new Color(1f, 1f, 1f, 0.95f));
        PlaceInZone(l.rectTransform, (bandCenter[i] - bandHalf) * w, 0f, 3f, h + 6f);
        var r = AcUi.Image(zone, $"BandR{i}", new Color(1f, 1f, 1f, 0.95f));
        PlaceInZone(r.rectTransform, (bandCenter[i] + bandHalf) * w, 0f, 3f, h + 6f);
    }

    protected override void OnTurnStart(int i)
    {
        charge = 0f;
        charging = false;
        UpdateFill(i);
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
                charge = 1f;          // 溜めすぎ=破裂
                UpdateFill(i);
                return TurnResult.Miss;
            }
            UpdateFill(i);
            return TurnResult.Continue;
        }

        // 離した
        bool hit = Mathf.Abs(charge - bandCenter[i]) <= bandHalf;
        return hit ? TurnResult.Hit : TurnResult.Miss;
    }

    protected override void OnTurnEnd(int i, bool hit)
    {
        if (fillImg[i] == null) return;
        if (hit)
        {
            if (bandImg[i] != null) bandImg[i].color = new Color(1f, 0.92f, 0.35f, 0.6f);
        }
        else
        {
            var c = fillImg[i].color;
            fillImg[i].color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f);
        }
    }

    private void UpdateFill(int i)
    {
        if (fillRt[i] == null) return;
        fillRt[i].sizeDelta = new Vector2(ZoneInnerW * charge, fillRt[i].sizeDelta.y);
    }
}
