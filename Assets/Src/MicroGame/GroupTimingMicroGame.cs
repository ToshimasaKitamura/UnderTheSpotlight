using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// グループ型アクションコマンド「タイミング！」。
/// 手番プレイヤーの領域を白いマーカーが左右に往復する。光る「ねらい枠」に重なった瞬間に SPACE で命中。
/// 押せるのは手番につき1回だけ。外したら不発、時間切れも不発。
/// 難易度: speedMul=マーカー速度 / rangeScale=ねらい枠の広さ / interference=マーカーの点滅(見えにくさ)。
/// </summary>
public class GroupTimingMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "タイミング！";
    protected override string PromptText => "ひかる わくで SPACE！";

    private const float MarkerW = 6f;

    private RectTransform[] markerRt;
    private Image[] markerImg;
    private Image[] windowImg;
    private float[] windowCenter;   // ねらい枠の中心(領域幅に対する 0..1)
    private float windowHalf;       // ねらい枠の半幅(0..1)
    private float sweepSpeed;       // マーカー速度(領域幅/秒)
    private float interference;

    private float markerPos;        // 0..1
    private float markerDir;
    private bool stopped;

    protected override void OnSetup(MicroGameContext c)
    {
        sweepSpeed = 0.9f * Mathf.Max(0.5f, c.speedMul);
        // ねらい枠は速いほど狭く。rangeScale で広げられる(カード効果ノブ)。
        windowHalf = Mathf.Clamp(0.11f * c.rangeScale / Mathf.Max(1f, 0.8f + 0.2f * c.speedMul), 0.04f, 0.2f);
        interference = Mathf.Clamp01(c.interference);

        markerRt = new RectTransform[PlayerCount];
        markerImg = new Image[PlayerCount];
        windowImg = new Image[PlayerCount];
        windowCenter = new float[PlayerCount];
        var rng = c.rng ?? new System.Random();
        for (int i = 0; i < PlayerCount; i++)
        {
            // 左端付近は開始直後に重なってしまうため、中央〜右寄りに置く
            windowCenter[i] = Mathf.Lerp(0.35f + windowHalf, 0.95f - windowHalf, (float)rng.NextDouble());
        }
    }

    protected override void BuildZone(int i, RectTransform zone, Color col)
    {
        float w = ZoneInnerW;
        float h = ZoneInnerH;

        var win = AcUi.Image(zone, $"Window{i}", new Color(col.r, col.g, col.b, 0.85f));
        PlaceInZone(win.rectTransform, windowCenter[i] * w, 0f, windowHalf * 2f * w, h - 6f);
        windowImg[i] = win;

        var m = AcUi.Image(zone, $"Marker{i}", new Color(1f, 1f, 1f, 0f)); // 手番まで非表示
        PlaceInZone(m.rectTransform, 0f, 0f, MarkerW, h + 4f);
        markerRt[i] = m.rectTransform;
        markerImg[i] = m;
    }

    protected override void OnTurnStart(int i)
    {
        markerPos = 0f;
        markerDir = 1f;
        stopped = false;
        SetMarker(i, 1f);
    }

    protected override TurnResult TickTurn(int i, float dt)
    {
        if (stopped) return TurnResult.Continue;

        markerPos += markerDir * sweepSpeed * dt;
        if (markerPos >= 1f) { markerPos = 2f - markerPos; markerDir = -1f; }
        if (markerPos <= 0f) { markerPos = -markerPos; markerDir = 1f; }
        markerPos = Mathf.Clamp01(markerPos);

        // 視覚妨害: 一定周期でマーカーが薄くなる
        float alpha = 1f;
        if (interference > 0f && Mathf.Sin(PlayerTime * 11f) > 0.3f) alpha = 1f - 0.85f * interference;
        SetMarker(i, alpha);

        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            stopped = true;
            SetMarker(i, 1f);
            bool hit = Mathf.Abs(markerPos - windowCenter[i]) <= windowHalf;
            return hit ? TurnResult.Hit : TurnResult.Miss;
        }
        return TurnResult.Continue;
    }

    protected override void OnTurnEnd(int i, bool hit)
    {
        // 止めた位置を残して見せる。外したら枠を暗くする
        if (!hit && windowImg[i] != null)
        {
            var c = windowImg[i].color;
            windowImg[i].color = new Color(c.r * 0.4f, c.g * 0.4f, c.b * 0.4f, 0.6f);
        }
        if (markerImg[i] != null) markerImg[i].color = hit ? new Color(1f, 0.92f, 0.35f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
    }

    private void SetMarker(int i, float alpha)
    {
        if (markerRt[i] == null) return;
        markerRt[i].anchoredPosition = new Vector2(markerPos * ZoneInnerW, 0f);
        if (markerImg[i] != null) markerImg[i].color = new Color(1f, 1f, 1f, alpha);
    }
}
