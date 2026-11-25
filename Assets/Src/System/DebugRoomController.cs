using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// デバッグ室。ターミナル風のテキスト画面。↑↓で選び(選択行が反転)、Enter で起動する。
///
/// メイン: 実装済みボスの「弾幕」をステージ/何ボス目つきでリスト化。
///   未作成ステージの仮ボス(ハムスター)と、同一ボスの重複は出さない。
/// 下部: アクションコマンド / キャラ登場 / 通常プレイ / タイトル。
/// バトル中は Esc でこのデバッグ室へ戻る。
/// </summary>
public class DebugRoomController : MonoBehaviour
{
    [Tooltip("弾幕リストの元になる全ステージ(DebugRoomSetup が自動割当)。")]
    [SerializeField] private StageData[] stages;

    [Tooltip("弾幕以外(AC/キャラ/通常)で使う既定ステージ。")]
    [SerializeField] private StageData defaultStage;

    private struct Entry { public string label; public System.Action run; }
    private readonly List<Entry> _entries = new List<Entry>();
    private int _sel;
    private int _firstVariant = -1; // 弾種・パターン検証セクションの開始 index
    private int _firstMode = -1;    // モードセクションの開始 index

    /// <summary>
    /// ステージ/ボスに割り当てていない、弾幕の検証用バリエーション定義。
    /// さまざまなキャラ(AllChara_N)で、弾サイズ(小/中/大)・形状(丸/クナイ)・
    /// 狙い(自機狙い/自機外し/固定)・全方位/扇・偶奇way・時間変化フェーズを一通り確認する。
    /// </summary>
    private struct Variant
    {
        public string name, tag, sprite;
        public DanmakuPhase[] phases;
    }

    // DanmakuPhase 省略記法用のショートエイリアス
    private const DanmakuPatternType RING = DanmakuPatternType.Ring;
    private const DanmakuPatternType FAN = DanmakuPatternType.AimedFan;

    private static readonly Variant[] Variants =
    {
        new Variant{ name="小弾シャワー(全方位)", tag="ring/small",   sprite="AllChara_5", phases=new[]{
            new DanmakuPhase{ shape=RING, aim=DanmakuAim.FixedDown, wayCount=28, rotationStepDeg=7f,  fireInterval=0.33f, bulletSpeed=3.2f, kind=BulletKind.Normal, size=BulletSize.Small,  bulletShape=BulletShape.Round, duration=1.2f } } },

        new Variant{ name="中弾リング・自機狙い(奇数)", tag="ring/aim-odd", sprite="AllChara_12", phases=new[]{
            new DanmakuPhase{ shape=RING, aim=DanmakuAim.Aimed, wayCount=13, fireInterval=0.70f, bulletSpeed=3.0f, kind=BulletKind.Normal, size=BulletSize.Medium, bulletShape=BulletShape.Round, duration=1.4f } } },

        new Variant{ name="中弾リング・自機外し(偶数)", tag="ring/anti-even", sprite="AllChara_20", phases=new[]{
            new DanmakuPhase{ shape=RING, aim=DanmakuAim.AntiAim, wayCount=12, fireInterval=0.70f, bulletSpeed=3.0f, kind=BulletKind.Normal, size=BulletSize.Medium, bulletShape=BulletShape.Round, duration=1.4f } } },

        new Variant{ name="大玉ゆったり全方位", tag="ring/large", sprite="AllChara_118", phases=new[]{
            new DanmakuPhase{ shape=RING, aim=DanmakuAim.FixedDown, wayCount=10, rotationStepDeg=11f, fireInterval=0.90f, bulletSpeed=1.8f, kind=BulletKind.Normal, size=BulletSize.Large,  bulletShape=BulletShape.Round, duration=1.8f } } },

        new Variant{ name="クナイ自機狙い連射", tag="fan/kunai", sprite="AllChara_48", phases=new[]{
            new DanmakuPhase{ shape=FAN, aim=DanmakuAim.Aimed, wayCount=5, fanSpreadDeg=20f, fireInterval=0.42f, bulletSpeed=5.6f, kind=BulletKind.Fast, size=BulletSize.Medium, bulletShape=BulletShape.Kunai, duration=1.4f } } },

        new Variant{ name="扇スイープ(中弾)", tag="fan/sweep", sprite="AllChara_33", phases=new[]{
            new DanmakuPhase{ shape=FAN, aim=DanmakuAim.FixedDown, wayCount=7, fanSpreadDeg=42f, rotationStepDeg=17f, fireInterval=0.22f, bulletSpeed=3.4f, kind=BulletKind.Normal, size=BulletSize.Medium, bulletShape=BulletShape.Round, duration=2.0f } } },

        new Variant{ name="小弾うずまき(高密度)", tag="ring/spiral", sprite="AllChara_72", phases=new[]{
            new DanmakuPhase{ shape=RING, aim=DanmakuAim.FixedDown, wayCount=24, rotationStepDeg=13f, fireInterval=0.08f, bulletSpeed=3.0f, kind=BulletKind.Normal, size=BulletSize.Small, bulletShape=BulletShape.Round, duration=1.6f } } },

        new Variant{ name="回復弾まじり全方位", tag="ring/heal", sprite="AllChara_90", phases=new[]{
            new DanmakuPhase{ shape=RING, aim=DanmakuAim.FixedDown, wayCount=20, fireInterval=0.50f, bulletSpeed=2.6f, kind=BulletKind.Normal, size=BulletSize.Medium, bulletShape=BulletShape.Round, healEveryNShots=6, duration=1.5f } } },

        // --- 時間変化(複数フェーズ) ---
        new Variant{ name="自機狙い→破裂・自機外し全方位", tag="phase/burst", sprite="AllChara_60", phases=new[]{
            new DanmakuPhase{ label="aim",   shape=FAN,  aim=DanmakuAim.Aimed,   wayCount=3,  fanSpreadDeg=8f,  fireInterval=0.30f, bulletSpeed=4.5f, kind=BulletKind.Fast,   size=BulletSize.Medium, bulletShape=BulletShape.Kunai, duration=1.2f },
            new DanmakuPhase{ label="burst", shape=RING, aim=DanmakuAim.AntiAim, wayCount=16, fireInterval=0.50f, bulletSpeed=3.2f, kind=BulletKind.Normal, size=BulletSize.Large,  bulletShape=BulletShape.Round, duration=0.6f } } },

        new Variant{ name="溜め大玉→小弾ばらまき", tag="phase/charge", sprite="AllChara_100", phases=new[]{
            new DanmakuPhase{ label="charge",  shape=RING, aim=DanmakuAim.FixedDown, wayCount=6,  fireInterval=0.60f, bulletSpeed=1.6f, kind=BulletKind.Normal, size=BulletSize.Large, bulletShape=BulletShape.Round, duration=1.8f },
            new DanmakuPhase{ label="scatter", shape=RING, aim=DanmakuAim.FixedDown, wayCount=32, rotationStepDeg=9f, fireInterval=0.14f, bulletSpeed=3.4f, kind=BulletKind.Normal, size=BulletSize.Small, bulletShape=BulletShape.Round, duration=1.2f } } },

        new Variant{ name="自機狙い扇↔全方位(複合)", tag="phase/altern", sprite="AllChara_7", phases=new[]{
            new DanmakuPhase{ label="aimfan", shape=FAN,  aim=DanmakuAim.Aimed,     wayCount=7,  fanSpreadDeg=30f, fireInterval=0.30f, bulletSpeed=3.5f, kind=BulletKind.Normal, size=BulletSize.Medium, bulletShape=BulletShape.Round, duration=1.0f },
            new DanmakuPhase{ label="ring",   shape=RING, aim=DanmakuAim.FixedDown, wayCount=18, rotationStepDeg=7f, fireInterval=0.40f, bulletSpeed=3.0f, kind=BulletKind.Normal, size=BulletSize.Medium, bulletShape=BulletShape.Round, duration=1.0f } } },

        new Variant{ name="クナイ回転→特大玉狙い", tag="phase/mix2", sprite="AllChara_25", phases=new[]{
            new DanmakuPhase{ label="spin", shape=RING, aim=DanmakuAim.FixedDown, wayCount=10, rotationStepDeg=14f, fireInterval=0.12f, bulletSpeed=4.5f, kind=BulletKind.Fast, size=BulletSize.Medium, bulletShape=BulletShape.Kunai, duration=1.5f },
            new DanmakuPhase{ label="orb",  shape=FAN,  aim=DanmakuAim.Aimed,     wayCount=1,  fanSpreadDeg=0f,  fireInterval=0.50f, bulletSpeed=2.0f, kind=BulletKind.Normal, size=BulletSize.Large,  bulletShape=BulletShape.Round, duration=1.0f } } },
    };

    // ターミナル配色
    private static readonly Color Bg = new Color(0.02f, 0.03f, 0.02f, 1f);
    private static readonly Color Fg = new Color(0.40f, 0.95f, 0.45f, 1f);  // 緑
    private static readonly Color Dim = new Color(0.22f, 0.55f, 0.26f, 1f);
    private static readonly Color SelBg = new Color(0.40f, 0.95f, 0.45f, 1f);
    private static readonly Color SelFg = new Color(0.02f, 0.03f, 0.02f, 1f);

    private GUIStyle _line, _lineDim, _lineSel;
    private Texture2D _px;
    private bool _ready;

    private void OnEnable() => BuildEntries();

    /// <summary>項目リストを組み立てる。弾幕=実ボスのみ(仮ボス/重複を除外)、続いて各モード。</summary>
    private void BuildEntries()
    {
        _entries.Clear();
        var seen = new HashSet<EnemyData>();
        if (stages != null)
        {
            for (int s = 0; s < stages.Length; s++)
            {
                var st = stages[s];
                if (st == null || st.bosses == null) continue;
                for (int b = 0; b < st.bosses.Count; b++)
                {
                    var boss = st.bosses[b];
                    if (boss == null) continue;
                    // 未作成ステージの仮ボス(ハムスター)はリストに出さない
                    if (boss.id == "hamster" || boss.displayName == "ハムスター") continue;
                    if (!seen.Add(boss)) continue; // 同一ボスの重複は1つだけ
                    var capStage = st; var capIdx = b;
                    string tag = $"{st.name.ToLowerInvariant()}/b{b + 1}";
                    _entries.Add(new Entry
                    {
                        label = $"danmaku  {tag,-14} {boss.displayName}",
                        run = () => LaunchDanmaku(capStage, capIdx),
                    });
                }
            }
        }

        // 弾種・パターン検証(ステージ/ボス未割当)
        _firstVariant = _entries.Count;
        for (int i = 0; i < Variants.Length; i++)
        {
            var v = Variants[i];
            _entries.Add(new Entry
            {
                label = $"variant  {v.tag,-12} {v.name}",
                run = () => LaunchVariant(v),
            });
        }

        _firstMode = _entries.Count;
        _entries.Add(new Entry { label = "mode     action-command", run = () => Launch(DebugLaunch.Mode.ActionCommand) });
        _entries.Add(new Entry { label = "mode     character-intro", run = () => Launch(DebugLaunch.Mode.Character) });
        _entries.Add(new Entry { label = "mode     full-battle", run = () => Launch(DebugLaunch.Mode.None) });
        _entries.Add(new Entry { label = "exit     title", run = GoTitle });

        _sel = Mathf.Clamp(_sel, 0, Mathf.Max(0, _entries.Count - 1));
    }

    private void Update()
    {
        if (_entries.Count == 0) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.downArrowKey.wasPressedThisFrame)
            _sel = (_sel + 1) % _entries.Count;
        if (kb.upArrowKey.wasPressedThisFrame)
            _sel = (_sel - 1 + _entries.Count) % _entries.Count;
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            _entries[_sel].run?.Invoke();
    }

    private void EnsureStyles()
    {
        if (_ready) return;
        var font = GameFont.Body();
        _line = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, font = font, richText = false };
        _line.normal.textColor = Fg;
        _lineDim = new GUIStyle(_line); _lineDim.normal.textColor = Dim;
        _lineSel = new GUIStyle(_line); _lineSel.normal.textColor = SelFg;
        _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply();
        _ready = true;
    }

    private void Fill(Rect r, Color c)
    {
        var g = GUI.color; GUI.color = c; GUI.DrawTexture(r, _px); GUI.color = g;
    }

    private void OnGUI()
    {
        EnsureStyles();

        Fill(new Rect(0, 0, Screen.width, Screen.height), Bg);

        const float x = 48f, lh = 25f;
        float w = Screen.width - x * 2f;
        float y = 24f;

        GUI.Label(new Rect(x, y, w, lh), "under-the-spotlight : debug console", _lineDim); y += lh;
        GUI.Label(new Rect(x, y, w, lh), new string('-', 46), _lineDim); y += lh + 6f;

        for (int i = 0; i < _entries.Count; i++)
        {
            // セクション見出し(非選択のコメント行)
            if (i == _firstVariant)
            {
                y += 6f;
                GUI.Label(new Rect(x, y, w, lh), "# 弾種・パターン検証 (ステージ/ボス未割当)", _lineDim); y += lh;
            }
            if (i == _firstMode)
            {
                y += 6f;
                GUI.Label(new Rect(x, y, w, lh), "# モード", _lineDim); y += lh;
            }

            bool sel = i == _sel;
            var row = new Rect(x - 10f, y, w + 20f, lh);
            if (sel) Fill(row, SelBg);
            string prefix = sel ? " > " : "   ";
            GUI.Label(new Rect(x, y, w, lh), prefix + _entries[i].label, sel ? _lineSel : _line);
            y += lh;
        }

        y += lh;
        GUI.Label(new Rect(x, y, w, lh), "# esc: back to this console (in battle)", _lineDim);
    }

    /// <summary>指定ステージ・ボスの弾幕(回避)を単体起動する。</summary>
    private void LaunchDanmaku(StageData stage, int bossIdx)
    {
        if (stage == null) return;
        DebugLaunch.Current = DebugLaunch.Mode.Danmaku;
        DebugLaunch.OverrideEnemy = null; // 実ボスなので上書きは使わない
        var gsm = GameSceneManager.Instance;
        if (gsm == null) { Debug.LogError("[DebugRoom] GameSceneManager が見つかりません。"); return; }
        gsm.selectedStage = stage;
        gsm.bossIndex = bossIdx;
        Debug.Log($"[DebugRoom] 弾幕: {stage.name} / B{bossIdx + 1}");
        gsm.GoToBattle(stage, bossIdx);
    }

    /// <summary>
    /// ステージ/ボスに割り当てていない弾種・パターン検証用の弾幕を起動する。
    /// 実行時に EnemyData を組み立てて DebugLaunch.OverrideEnemy で注入し、既定ステージ上で回避ループに入る。
    /// </summary>
    private void LaunchVariant(Variant v)
    {
        var gsm = GameSceneManager.Instance;
        if (gsm == null) { Debug.LogError("[DebugRoom] GameSceneManager が見つかりません。"); return; }
        var stage = defaultStage != null ? defaultStage : (stages != null && stages.Length > 0 ? stages[0] : null);
        if (stage == null) { Debug.LogError("[DebugRoom] 既定ステージが未設定です。DebugRoomSetup で defaultStage を割り当ててください。"); return; }

        var e = ScriptableObject.CreateInstance<EnemyData>();
        e.hideFlags = HideFlags.HideAndDontSave; // シーン遷移/未使用アセット回収で消えないように
        e.id = "dbg_" + v.sprite;
        e.displayName = v.name;
        e.bossSpriteName = v.sprite;
        e.danmakuPhases = new List<DanmakuPhase>(v.phases); // フェーズ列が単一パラメータより優先される
        e.parts = new List<EnemyPart> { new EnemyPart { id = "core", displayName = "本体", maxHp = 999 } };

        DebugLaunch.Current = DebugLaunch.Mode.Danmaku;
        DebugLaunch.OverrideEnemy = e;
        gsm.selectedStage = stage;
        gsm.bossIndex = 0;
        Debug.Log($"[DebugRoom] 弾種検証: {v.name} ({v.tag}, {v.sprite}) ※ステージ/ボス未割当");
        gsm.GoToBattle(stage, 0);
    }

    /// <summary>弾幕以外のモード(AC/キャラ/通常)を既定ステージで起動。</summary>
    private void Launch(DebugLaunch.Mode mode)
    {
        DebugLaunch.Current = mode;
        DebugLaunch.OverrideEnemy = null;
        var gsm = GameSceneManager.Instance;
        if (gsm == null) { Debug.LogError("[DebugRoom] GameSceneManager が見つかりません。"); return; }
        var stage = defaultStage != null ? defaultStage : (stages != null && stages.Length > 0 ? stages[0] : null);
        if (stage == null) { Debug.LogError("[DebugRoom] ステージが未設定です。"); return; }
        gsm.selectedStage = stage;
        gsm.bossIndex = 0;
        Debug.Log($"[DebugRoom] 起動モード={mode}, stage={stage.name}");
        gsm.GoToBattle(stage, 0);
    }

    private void GoTitle()
    {
        DebugLaunch.Clear();
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.GoToTitle();
    }
}
