using UnityEngine;

/// <summary>
/// ステージセレクト画面のオンライン・ロビー UI(IMGUI オーバーレイ)。
///  - 右上に「NET」アイコン。押すとメニュー(部屋を建てる / 部屋に入る)。
///  - 建てる: コードを発行し「コピー」できる。参加者が来ると 2P/3P/4P と埋まる。
///  - 入る:   コード入力欄。参加すると自分のスロット(2P/3P/4P)が割り当たる。
/// 実通信は <see cref="NetworkLobby"/>(UGS_NETCODE 有効時) 経由。未導入時は明確なエラーを表示する。
///
/// この UI は常時コンパイルされる(パッケージ非依存)。実トランスポートの有無は
/// <see cref="NetworkLobby.Available"/> と <see cref="INetworkLobby.LastError"/> で判断する。
/// </summary>
public class NetworkLobbyPanel : MonoBehaviour
{
    private enum PanelState { Closed, Menu, Host, Join }

    public bool IsOpen => _state != PanelState.Closed;

    private PanelState _state = PanelState.Closed;
    private string _codeInput = string.Empty;
    private float _copiedFlash;     // 「コピーしました」表示の残り秒
    private INetworkLobby Lobby => NetworkLobby.Instance;

    // スタイル(遅延生成)
    private GUIStyle _title, _body, _btn, _codeBig, _slot, _err;
    private bool _stylesReady;

    private void Update()
    {
        if (NetworkLobby.Available) Lobby.Tick();
        if (_copiedFlash > 0f) _copiedFlash -= Time.deltaTime;

        // N キーでもトグル(アイコンのクリックと併用)
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.nKey.wasPressedThisFrame)
            _state = _state == PanelState.Closed ? PanelState.Menu : PanelState.Closed;

        // アクティブなネット・ロビーの参加状況を下流(キャラ選択/バトル)へ反映
        if (NetworkLobby.Available && Lobby.Status == NetLobbyStatus.Active && GameSceneManager.Instance != null)
        {
            var jp = GameSceneManager.Instance.joinedPlayers;
            var occ = Lobby.SlotOccupied;
            for (int i = 0; i < 4 && i < jp.Length && i < occ.Length; i++) jp[i] = occ[i];
        }
    }

    private void OnGUI()
    {
        GUI.depth = -20; // WorldMapView(depth 0)より前面に
        EnsureStyles();

        DrawNetIcon();

        switch (_state)
        {
            case PanelState.Menu: DrawMenu(); break;
            case PanelState.Host: DrawHost(); break;
            case PanelState.Join: DrawJoin(); break;
        }
    }

    // ---- 右上アイコン ----
    private void DrawNetIcon()
    {
        float w = 92f, h = 40f;
        var r = new Rect(Screen.width - w - 16f, 14f, w, h);
        bool active = NetworkLobby.Available && Lobby.Status == NetLobbyStatus.Active;
        string label = active ? "NET●" : "NET";
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = active ? GameColorScheme.Success : GameColorScheme.Sub;
        if (GUI.Button(r, label, _btn))
            _state = _state == PanelState.Closed ? PanelState.Menu : PanelState.Closed;
        GUI.backgroundColor = prev;
    }

    // ---- メニュー ----
    private void DrawMenu()
    {
        var box = CenterBox(420f, 300f);
        GUI.Box(box, "オンライン対戦", _title);

        float bx = box.x + 40f, bw = box.width - 80f;
        float by = box.y + 90f;

        if (GUI.Button(new Rect(bx, by, bw, 56f), "部屋を建てる", _btn))
        {
            _state = PanelState.Host;
            Lobby.HostRoom(4);
        }
        if (GUI.Button(new Rect(bx, by + 72f, bw, 56f), "部屋に入る", _btn))
        {
            _codeInput = string.Empty;
            _state = PanelState.Join;
        }
        if (GUI.Button(new Rect(bx, by + 156f, bw, 44f), "閉じる", _btn))
            _state = PanelState.Closed;

        if (!NetworkLobby.Available)
            GUI.Label(new Rect(box.x + 20f, box.yMax - 40f, box.width - 40f, 34f),
                "※ オンラインは未有効(パッケージ/UGS 設定が必要)", _err);
    }

    // ---- 部屋を建てる ----
    private void DrawHost()
    {
        var box = CenterBox(520f, 420f);
        GUI.Box(box, "部屋を建てる", _title);

        float cx = box.x + 30f, cw = box.width - 60f;
        float y = box.y + 84f;

        GUI.Label(new Rect(cx, y, cw, 30f), StatusLine(), _body);
        y += 40f;

        // 発行コード
        GUI.Label(new Rect(cx, y, cw, 28f), "参加コード", _body); y += 32f;
        string code = string.IsNullOrEmpty(Lobby.JoinCode) ? "— — — —" : Lobby.JoinCode;
        GUI.Box(new Rect(cx, y, cw - 130f, 56f), code, _codeBig);
        using (new GuiEnabled(!string.IsNullOrEmpty(Lobby.JoinCode)))
        {
            if (GUI.Button(new Rect(cx + cw - 120f, y, 120f, 56f), "コピー", _btn))
            {
                GUIUtility.systemCopyBuffer = Lobby.JoinCode;
                _copiedFlash = 1.6f;
            }
        }
        y += 64f;
        if (_copiedFlash > 0f)
            GUI.Label(new Rect(cx, y, cw, 24f), "コピーしました", _body);
        y += 30f;

        DrawRoster(new Rect(cx, y, cw, 120f));

        if (!string.IsNullOrEmpty(Lobby.LastError))
            GUI.Label(new Rect(cx, box.yMax - 84f, cw, 40f), Lobby.LastError, _err);

        if (GUI.Button(new Rect(cx, box.yMax - 42f, cw, 34f), "退出して閉じる", _btn))
        {
            Lobby.Leave();
            _state = PanelState.Closed;
        }
    }

    // ---- 部屋に入る ----
    private void DrawJoin()
    {
        var box = CenterBox(520f, 420f);
        GUI.Box(box, "部屋に入る", _title);

        float cx = box.x + 30f, cw = box.width - 60f;
        float y = box.y + 84f;

        GUI.Label(new Rect(cx, y, cw, 30f), StatusLine(), _body);
        y += 40f;

        GUI.Label(new Rect(cx, y, cw, 28f), "参加コードを入力", _body); y += 32f;
        GUI.SetNextControlName("codeField");
        _codeInput = GUI.TextField(new Rect(cx, y, cw, 52f), _codeInput ?? string.Empty, 16, _codeBig);
        _codeInput = _codeInput.Trim().ToUpperInvariant();
        y += 64f;

        bool canJoin = !string.IsNullOrEmpty(_codeInput) &&
                       Lobby.Status != NetLobbyStatus.Connecting;
        using (new GuiEnabled(canJoin))
        {
            if (GUI.Button(new Rect(cx, y, cw, 50f), "この コードで参加", _btn))
                Lobby.JoinRoom(_codeInput);
        }
        y += 66f;

        DrawRoster(new Rect(cx, y, cw, 90f));

        if (!string.IsNullOrEmpty(Lobby.LastError))
            GUI.Label(new Rect(cx, box.yMax - 84f, cw, 40f), Lobby.LastError, _err);

        if (GUI.Button(new Rect(cx, box.yMax - 42f, cw * 0.48f, 34f), "戻る", _btn))
            _state = PanelState.Menu;
        if (GUI.Button(new Rect(cx + cw * 0.52f, box.yMax - 42f, cw * 0.48f, 34f), "退出して閉じる", _btn))
        {
            Lobby.Leave();
            _state = PanelState.Closed;
        }
    }

    // ---- 参加者ロスター(P1..P4) ----
    private void DrawRoster(Rect area)
    {
        var occ = Lobby.SlotOccupied;
        int local = Lobby.LocalSlot;
        float cellW = area.width / 4f;
        for (int i = 0; i < 4; i++)
        {
            var cell = new Rect(area.x + cellW * i + 4f, area.y, cellW - 8f, area.height);
            var pcol = (i < GameColorScheme.Players.Length) ? GameColorScheme.Players[i] : Color.white;
            bool on = occ != null && i < occ.Length && occ[i];

            var prev = GUI.color;
            GUI.color = on ? pcol : new Color(0.4f, 0.4f, 0.46f, 1f);
            GUI.Box(cell, GUIContent.none);
            GUI.color = prev;

            string who = on ? (i == local ? "あなた" : "参加") : "空き";
            GUI.Label(new Rect(cell.x, cell.y + 8f, cell.width, 30f), $"P{i + 1}", _slot);
            GUI.Label(new Rect(cell.x, cell.y + cell.height - 36f, cell.width, 28f), who, _body);
        }
    }

    private string StatusLine()
    {
        if (!NetworkLobby.Available) return "オンライン: 未有効";
        switch (Lobby.Status)
        {
            case NetLobbyStatus.Connecting: return "接続中…";
            case NetLobbyStatus.Active:
                return Lobby.Role == NetLobbyRole.Host
                    ? $"ホスト稼働中 (参加 {Lobby.ConnectedCount} 人)"
                    : $"参加中: あなたは P{Lobby.LocalSlot + 1} (参加 {Lobby.ConnectedCount} 人)";
            case NetLobbyStatus.Error: return "エラー";
            default: return "待機";
        }
    }

    // ---- レイアウト/スタイル ----
    private Rect CenterBox(float w, float h)
    {
        // 半透明の暗幕
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;
        return new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
    }

    private void EnsureStyles()
    {
        if (_stylesReady) return;
        var font = GameFont.Body();

        _title = new GUIStyle(GUI.skin.box) { fontSize = 26, alignment = TextAnchor.UpperCenter, font = font };
        _title.padding = new RectOffset(10, 10, 16, 10);
        _title.normal.textColor = GameColorScheme.Accent;

        _body = new GUIStyle(GUI.skin.label) { fontSize = 18, font = font };
        _body.normal.textColor = GameColorScheme.TextOnDark;

        _btn = new GUIStyle(GUI.skin.button) { fontSize = 20, font = font };
        _btn.normal.textColor = GameColorScheme.TextOnDark;

        _codeBig = new GUIStyle(GUI.skin.box) { fontSize = 30, alignment = TextAnchor.MiddleCenter, font = font };
        _codeBig.normal.textColor = GameColorScheme.Accent;

        _slot = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperCenter, font = font };
        _slot.normal.textColor = Color.white;

        _err = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, font = font };
        _err.normal.textColor = GameColorScheme.Danger;

        _stylesReady = true;
    }

    /// <summary>using で GUI.enabled を一時変更するためのヘルパ。</summary>
    private readonly struct GuiEnabled : System.IDisposable
    {
        private readonly bool _prev;
        public GuiEnabled(bool enabled) { _prev = GUI.enabled; GUI.enabled = enabled; }
        public void Dispose() { GUI.enabled = _prev; }
    }
}
