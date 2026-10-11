using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム中に D キーで表示/非表示を切り替えるデバッグパネル(IMGUI)。
/// 味方HP(共有) / 敵部位HP / 経過ラウンド数 / 選択カードの効果値 / AC各プレイヤーのダメージ を表示する。
/// RuntimeInitializeOnLoadMethod で自動生成し、DontDestroyOnLoad で常駐する(シーンに配置不要)。
/// 日本語を含むため IMGUI フォントは GameFont.Body() を使う(CLAUDE.md 参照)。
/// </summary>
public class BattleDebugPanel : MonoBehaviour
{
    private static BattleDebugPanel instance;
    private bool visible;

    // 経過ラウンド(CardSelect フェーズに入るたびに加算)
    private int roundCount;
    private bool inBattle;
    private bool hasLastState;
    private BattleStateMachine.State lastState;

    // キャッシュ参照(Update で更新)
    private BattleStateMachine sm;
    private SharedHpController sharedHp;
    private EnemyRuntimeState enemyState;
    private CardSelectPhase cardSelect;
    private ActionCommandPhase actionCommand;

    private GUIStyle style;
    private Texture2D bgTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[BattleDebugPanel]");
        instance = go.AddComponent<BattleDebugPanel>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        // 新 Input System。D キーでトグル。
        var kb = Keyboard.current;
        if (kb != null && kb.dKey.wasPressedThisFrame) visible = !visible;

        sm = FindObjectOfType<BattleStateMachine>();
        if (sm == null)
        {
            inBattle = false;
            hasLastState = false;
            sharedHp = null; enemyState = null; cardSelect = null; actionCommand = null;
            return;
        }

        // バトルに入った瞬間にラウンドを1へ
        if (!inBattle) { inBattle = true; roundCount = 1; hasLastState = false; }

        var st = sm.CurrentState;
        if (hasLastState && st == BattleStateMachine.State.CardSelect && lastState != BattleStateMachine.State.CardSelect)
            roundCount++;
        lastState = st;
        hasLastState = true;

        sharedHp = FindObjectOfType<SharedHpController>();
        enemyState = FindObjectOfType<EnemyRuntimeState>();
        cardSelect = FindObjectOfType<CardSelectPhase>();
        actionCommand = FindObjectOfType<ActionCommandPhase>();
    }

    private void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.font = GameFont.Body(); // 日本語対応フォント(OSダイナミックは使わない)
            style.normal.textColor = Color.white;
            style.richText = false;
            style.wordWrap = false;
        }
        if (bgTex == null)
        {
            bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, Color.white);
            bgTex.Apply();
        }

        string text = BuildText();

        Vector2 content = style.CalcSize(new GUIContent(text));
        float pad = 10f;
        var rect = new Rect(10, 10, content.x + pad * 2, content.y + pad * 2);

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(rect, bgTex);
        GUI.color = prev;

        GUI.Label(new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2, rect.height - pad * 2), text, style);
    }

    private string BuildText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== DEBUG (D でトグル) ===");
        sb.AppendLine($"State: {(sm != null ? sm.CurrentState.ToString() : "-")}   経過ラウンド: {roundCount}");

        // 味方HP(共有)
        sb.AppendLine(sharedHp != null
            ? $"味方HP(共有): {sharedHp.CurrentHp} / {sharedHp.maxHp}"
            : "味方HP: -");

        // 敵HP(部位)
        if (enemyState != null && enemyState.enemyData != null)
        {
            var ed = enemyState.enemyData;
            sb.AppendLine($"敵: {ed.displayName} ({ed.id})");
            if (ed.parts != null)
            {
                foreach (var p in ed.parts)
                {
                    if (p == null) continue;
                    int cur = enemyState.GetCurrentHp(p.id);
                    bool dead = enemyState.IsDestroyed(p.id);
                    sb.AppendLine($"  {p.displayName}: {cur}/{p.maxHp}{(dead ? " [破壊]" : "")}");
                }
            }
        }
        else
        {
            sb.AppendLine("敵: -");
        }

        // 選択カードの効果値(各プレイヤー)
        if (cardSelect != null && cardSelect.Selections != null && cardSelect.Selections.Count > 0)
        {
            sb.AppendLine("選択カード:");
            var sel = cardSelect.Selections;
            for (int i = 0; i < sel.Count; i++)
            {
                var c = sel[i];
                if (c == null || c.Data == null) { sb.AppendLine($"  P{i + 1}: -"); continue; }
                string nm = string.IsNullOrEmpty(c.Data.displayName) ? c.Data.name : c.Data.displayName;
                sb.AppendLine($"  P{i + 1}: {nm}  威力{c.EffectivePower} 難度{c.EffectiveDifficulty} [{c.Category}]");
            }
        }

        // AC 各プレイヤーのダメージ/成功
        if (actionCommand != null && actionCommand.PerPlayerDamage != null)
        {
            sb.AppendLine("AC ダメージ(各P):");
            var dmg = actionCommand.PerPlayerDamage;
            var succ = actionCommand.PerPlayerSuccess;
            for (int i = 0; i < dmg.Count; i++)
            {
                int s = (succ != null && i < succ.Count) ? succ[i] : 0;
                sb.AppendLine($"  P{i + 1}: {dmg[i]} dmg  (成功{s})");
            }
        }

        return sb.ToString();
    }
}
