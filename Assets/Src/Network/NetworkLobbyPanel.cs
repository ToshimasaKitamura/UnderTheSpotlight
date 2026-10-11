using UnityEngine;

/// <summary>
/// ステージセレクト画面のオンライン・ロビー UI(IMGUI オーバーレイ)。
///  - 右上に地球儀アイコン+「オンライン」ボタン。押すとメニュー(部屋を建てる / 部屋に入る)。
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
    private GUIStyle _title, _body, _btn, _codeBig, _slot, _err, _netLabel;
    private bool _stylesReady;

    // NET アイコン用テクスチャ(遅延生成・OnDestroy で破棄)
    private Texture2D _netPill, _netPillHover, _netGlobe, _netDot;

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

    // ---- 右上アイコン(グローブ型 + 「オンライン」文字 + 接続状態ドット) ----
    private void DrawNetIcon()
    {
        BuildNetTextures();

        const float w = 220f, h = 64f;
        var r = new Rect(Screen.width - w - 16f, 14f, w, h);
        bool active = NetworkLobby.Available && Lobby.Status == NetLobbyStatus.Active;
        bool highlight = _state != PanelState.Closed || r.Contains(Event.current.mousePosition);
        Color prevC = GUI.color;

        // 背景ピル(角丸)
        GUI.DrawTexture(r, highlight ? _netPillHover : _netPill, ScaleMode.StretchToFill, true);

        // グローブ・アイコン(主役。接続中は緑、未接続はゴールド)
        Color iconCol = active ? GameColorScheme.Success : GameColorScheme.Accent;
        const float ic = 46f;
        var iconRect = new Rect(r.x + 16f, r.y + (h - ic) * 0.5f, ic, ic);
        GUI.color = iconCol;
        GUI.DrawTexture(iconRect, _netGlobe, ScaleMode.StretchToFill, true);
        GUI.color = prevC;

        // 接続状態ドット(アイコン右上に小さく重ねる。接続中=緑点灯 / 未接続=灰)
        const float dot = 11f;
        var dotRect = new Rect(iconRect.xMax - 7f, iconRect.y - 1f, dot, dot);
        GUI.color = active ? GameColorScheme.Success : new Color(0.45f, 0.45f, 0.52f, 1f);
        GUI.DrawTexture(dotRect, _netDot, ScaleMode.StretchToFill, true);
        GUI.color = prevC;

        // 「オンライン」文字(はっきり見せる)
        _netLabel.normal.textColor = active ? GameColorScheme.Success : GameColorScheme.TextOnDark;
        var textRect = new Rect(iconRect.xMax + 10f, r.y, r.xMax - 12f - (iconRect.xMax + 10f), h);
        GUI.Label(textRect, "オンライン", _netLabel);

        // クリック判定(透明ボタンを全面に重ねる)
        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            _state = _state == PanelState.Closed ? PanelState.Menu : PanelState.Closed;
    }

    // ---- アイコン用テクスチャ生成(1回だけ) ----
    private void BuildNetTextures()
    {
        if (_netPill != null) return;
        _netPill      = MakeRoundedRect(150, 44, 13, new Color(0.141f, 0.078f, 0.212f, 0.85f), new Color(1f, 1f, 1f, 0.12f));
        _netPillHover = MakeRoundedRect(150, 44, 13, new Color(0.24f, 0.13f, 0.36f, 0.95f), GameColorScheme.Accent);
        _netGlobe     = MakeGlobe(72);
        _netDot       = MakeDisc(28);
    }

    /// <summary>角丸の塗りつぶし矩形(外周に淡いボーダー)。白ベースではなく指定色で生成。</summary>
    private static Texture2D MakeRoundedRect(int w, int h, float radius, Color fill, Color border)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 角丸矩形の縁からの内向き距離(中央/辺では十分大きく、角のみ丸める)
                float cx = Mathf.Clamp(x, radius, w - 1 - radius);
                float cy = Mathf.Clamp(y, radius, h - 1 - radius);
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float edge = radius - dist;              // >0 で内側(縁からの距離)
                float insideA = Mathf.Clamp01(edge + 0.5f);
                if (insideA <= 0f) { px[y * w + x] = new Color(0, 0, 0, 0); continue; }

                float b = Mathf.Clamp01(2.0f - edge);    // 外周 ~2px をボーダーに
                Color c = Color.Lerp(fill, border, b * border.a);
                c.a = Mathf.Max(fill.a * insideA, b * border.a * insideA);
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>地球儀(外周リング＋経線/緯線)アイコン。白で生成し、描画時に色を乗算する。</summary>
    private static Texture2D MakeGlobe(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        float c = (s - 1) * 0.5f;
        float R = c - 3f;
        float th = Mathf.Max(1.6f, s * 0.04f);   // 線の太さ
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool inside = d <= R - th * 0.4f;

                float ringA = Mathf.Clamp01(th - Mathf.Abs(d - R));                 // 外周リング
                float merA  = inside ? Mathf.Clamp01(th * 0.75f - Mathf.Abs(dx)) : 0f; // 経線(縦)
                float eqA   = inside ? Mathf.Clamp01(th * 0.75f - Mathf.Abs(dy)) : 0f; // 赤道(横)
                float lat1  = inside ? Mathf.Clamp01(th * 0.65f - Mathf.Abs(dy - R * 0.5f)) : 0f; // 緯線上
                float lat2  = inside ? Mathf.Clamp01(th * 0.65f - Mathf.Abs(dy + R * 0.5f)) : 0f; // 緯線下

                float a = Mathf.Max(ringA, Mathf.Max(merA, Mathf.Max(eqA, Mathf.Max(lat1, lat2))));
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>塗りつぶし円(アンチエイリアス)。白で生成し、描画時に色を乗算する。</summary>
    private static Texture2D MakeDisc(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        float c = (s - 1) * 0.5f;
        float rad = c - 1f;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(rad - d + 0.5f));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private void OnDestroy()
    {
        if (_netPill != null) Destroy(_netPill);
        if (_netPillHover != null) Destroy(_netPillHover);
        if (_netGlobe != null) Destroy(_netGlobe);
        if (_netDot != null) Destroy(_netDot);
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

        _netLabel = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.MiddleLeft, font = font, wordWrap = false, clipping = TextClipping.Overflow };
        _netLabel.normal.textColor = GameColorScheme.TextOnDark;

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
