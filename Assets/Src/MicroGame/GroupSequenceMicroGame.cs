using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// アクションコマンド「じゅんばんに！」(矢印パネル)。
/// フィールドに大きな矢印パネルが並ぶ。矢印キー(または WASD)で左から順に入力し、全部合えば命中。
/// 1回でも違う方向を押したら不発。時間切れも不発。
/// 矢印は三角の図形を回転させて描く(フォントの矢印グリフに依存しない)。
/// 難易度: buttonCount=パネル数(2〜6) / interference≥0.5=2つ先以降が「?」で隠れる。
/// </summary>
public class GroupSequenceMicroGame : GroupRelayMicroGame
{
    public override string DisplayName => "じゅんばんに！";
    protected override string PromptText => "やじるしキー(WASD)で じゅんばんに！";

    private const int MinLen = 2;
    private const int MaxLen = 6;
    private const float MaxCell = 96f;
    private const float Gap = 12f;
    // 0=上 1=下 2=左 3=右 の向きにする回転(三角は上向き。Unity の z 回転は反時計回り)
    private static readonly float[] DirAngle = { 0f, 180f, 90f, -90f };

    private int len;
    private float cell;
    private int[][] sequence;      // [player][step]
    private Image[] panels;
    private Image[] arrows;
    private Text[] hidden;
    private float interference;

    private int step;
    private Color col;

    protected override void OnSetup(MicroGameContext c)
    {
        len = Mathf.Clamp(c.buttonCount, MinLen, MaxLen);
        interference = Mathf.Clamp01(c.interference);
        cell = Mathf.Min(MaxCell, (PlayW - 40f - Gap * (len - 1)) / len);

        var rng = c.rng ?? new System.Random();
        sequence = new int[PlayerCount][];
        for (int i = 0; i < PlayerCount; i++)
        {
            sequence[i] = new int[len];
            for (int s = 0; s < len; s++) sequence[i][s] = rng.Next(4);
        }
    }

    protected override void BuildPlay(RectTransform play)
    {
        panels = new Image[len];
        arrows = new Image[len];
        hidden = new Text[len];
        float totalW = len * cell + (len - 1) * Gap;
        for (int s = 0; s < len; s++)
        {
            float x = -totalW / 2f + cell / 2f + s * (cell + Gap);
            panels[s] = AcUi.Image(play, $"Panel{s}", Color.white);
            PlaceCenter(panels[s].rectTransform, new Vector2(x, 0f), new Vector2(cell, cell));

            arrows[s] = Shape(panels[s].transform, $"Arrow{s}", AcShapes.Triangle(), Color.white,
                Vector2.zero, new Vector2(cell * 0.58f, cell * 0.5f));

            hidden[s] = AcUi.Label(panels[s].transform, $"Hidden{s}", Mathf.RoundToInt(cell * 0.6f), Color.white, TextAnchor.MiddleCenter, true, true);
            PlaceCenter(hidden[s].rectTransform, Vector2.zero, new Vector2(cell, cell));
            hidden[s].text = "?";
        }
    }

    protected override void OnTurnStart(int i, Color c)
    {
        col = c;
        step = 0;
        for (int s = 0; s < len; s++)
            arrows[s].rectTransform.localEulerAngles = new Vector3(0f, 0f, DirAngle[sequence[i][s]]);
        Refresh(i);
    }

    protected override TurnResult TickTurn(int i, float dt)
    {
        int pressed = ReadDirectionPressed();
        if (pressed < 0) return TurnResult.Continue;

        if (pressed != sequence[i][step])
        {
            panels[step].color = new Color(0.85f, 0.25f, 0.25f, 1f);
            ShowArrow(step, true);
            arrows[step].color = Color.white;
            return TurnResult.Miss;
        }

        step++;
        if (step >= len) return TurnResult.Hit;
        Refresh(i);
        return TurnResult.Continue;
    }

    protected override void OnTurnEnd(int i, bool hit)
    {
        if (!hit) return;
        for (int s = 0; s < len; s++)
        {
            panels[s].color = HitColor;
            arrows[s].color = new Color(0.12f, 0.10f, 0.05f, 1f);
        }
    }

    /// <summary>入力済み=暗い / いま押すパネル=明るく大きめ / これから=プレイヤー色(妨害時は「?」)。</summary>
    private void Refresh(int i)
    {
        for (int s = 0; s < len; s++)
        {
            float scale = 1f;
            if (s < step)
            {
                panels[s].color = new Color(0.20f, 0.20f, 0.26f, 1f);
                arrows[s].color = new Color(0.45f, 0.45f, 0.52f, 1f);
                ShowArrow(s, true);
            }
            else if (s == step)
            {
                panels[s].color = col;
                arrows[s].color = new Color(0.08f, 0.08f, 0.12f, 1f);
                ShowArrow(s, true);
                scale = 1.12f;
            }
            else
            {
                panels[s].color = Dim(col, 0.35f);
                arrows[s].color = col;
                bool hide = interference >= 0.5f && s > step + 1;
                ShowArrow(s, !hide);
            }
            panels[s].rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void ShowArrow(int s, bool show)
    {
        arrows[s].enabled = show;
        hidden[s].enabled = !show;
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
