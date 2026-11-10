using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// グループ型アクションコマンド「じゅんばんに！」。
/// 手番プレイヤーの領域に矢印が並ぶ。矢印キー(または WASD)で左から順に入力し、全部合えば命中。
/// 1回でも違うキーを押したら不発。時間切れも不発。
/// 難易度: buttonCount=矢印の数(2〜6) / interference=次以降の矢印を隠す(見えにくさ)。
/// </summary>
public class GroupSequenceMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "じゅんばんに！";
    protected override string PromptText => "やじるしを じゅんばんに！";

    private const int MinLen = 2;
    private const int MaxLen = 6;
    private static readonly string[] ArrowGlyph = { "↑", "↓", "←", "→" };

    private int[][] sequence;     // [player][step] = 0..3 (上下左右)
    private Text[][] arrowText;
    private int step;
    private float interference;

    protected override void OnSetup(MicroGameContext c)
    {
        int len = Mathf.Clamp(c.buttonCount, MinLen, MaxLen);
        interference = Mathf.Clamp01(c.interference);
        var rng = c.rng ?? new System.Random();

        sequence = new int[PlayerCount][];
        arrowText = new Text[PlayerCount][];
        for (int i = 0; i < PlayerCount; i++)
        {
            sequence[i] = new int[len];
            for (int s = 0; s < len; s++) sequence[i][s] = rng.Next(4);
            arrowText[i] = new Text[len];
        }
    }

    protected override void BuildZone(int i, RectTransform zone, Color col)
    {
        int len = sequence[i].Length;
        float cell = ZoneInnerW / len;
        int fontSize = Mathf.Clamp(Mathf.RoundToInt(cell * 0.75f), 18, 44);
        for (int s = 0; s < len; s++)
        {
            var t = AcUi.Label(zone, $"Arrow{i}_{s}", fontSize, Color.white, TextAnchor.MiddleCenter, true, true);
            t.text = ArrowGlyph[sequence[i][s]];
            PlaceInZone(t.rectTransform, cell * (s + 0.5f), 0f, cell, ZoneInnerH);
            arrowText[i][s] = t;
        }
        SetZoneColors(i, -1, col);
    }

    protected override void OnTurnStart(int i)
    {
        step = 0;
        SetZoneColors(i, step, PlayerColor(i));
    }

    protected override TurnResult TickTurn(int i, float dt)
    {
        int pressed = ReadDirectionPressed();
        if (pressed < 0) return TurnResult.Continue;

        if (pressed != sequence[i][step])
        {
            if (arrowText[i][step] != null) arrowText[i][step].color = new Color(1f, 0.3f, 0.3f, 1f);
            return TurnResult.Miss;
        }

        step++;
        if (step >= sequence[i].Length) return TurnResult.Hit;
        SetZoneColors(i, step, PlayerColor(i));
        return TurnResult.Continue;
    }

    protected override void OnTurnEnd(int i, bool hit)
    {
        if (!hit) return;
        foreach (var t in arrowText[i]) if (t != null) t.color = new Color(1f, 0.92f, 0.35f, 1f);
    }

    /// <summary>
    /// 矢印の色を更新。current&lt;0 は手番前(全部うすく表示)。
    /// 入力済み=暗く / いま押す矢印=白 / これから=プレイヤー色(妨害が強いと「？」で隠す)。
    /// </summary>
    private void SetZoneColors(int i, int current, Color col)
    {
        var texts = arrowText[i];
        for (int s = 0; s < texts.Length; s++)
        {
            var t = texts[s];
            if (t == null) continue;
            if (current < 0)
            {
                t.text = ArrowGlyph[sequence[i][s]];
                t.color = new Color(col.r, col.g, col.b, 0.45f);
            }
            else if (s < current)
            {
                t.color = new Color(0.35f, 0.35f, 0.4f, 1f);
            }
            else if (s == current)
            {
                t.text = ArrowGlyph[sequence[i][s]];
                t.color = Color.white;
            }
            else
            {
                // 視覚妨害: 2つ先以降を隠す(interference 0.5 以上)
                bool hide = interference >= 0.5f && s > current + 1;
                t.text = hide ? "？" : ArrowGlyph[sequence[i][s]];
                t.color = col;
            }
        }
    }

    /// <summary>このフレームに押された方向。0=上 1=下 2=左 3=右、無ければ -1。</summary>
    private static int ReadDirectionPressed()
    {
        var kb = Keyboard.current;
        if (kb == null) return -1;
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) return 0;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return 1;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return 2;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return 3;
        return -1;
    }
}
