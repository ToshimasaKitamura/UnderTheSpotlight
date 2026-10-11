using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ワールドマップシーン専用のコントローラー。
/// WorldMapPhase の機能を独立シーンとして動作させます。
/// キーボード/ゲームパッド両対応。
/// </summary>
public class WorldMapSceneController : MonoBehaviour
{
    [Header("マップデータ")]
    [Tooltip("表示するワールドマップデータ")]
    [SerializeField] private WorldMapData worldMap;

    [Header("ゲーム進行")]
    [Tooltip("ゲーム進行状態。シーン間で共有される")]
    [SerializeField] private RunProgress runProgress;

    private WorldMapView view;

    // ===== ロビー(プレイヤー募集)状態 =====
    // ローカル操作は自分のカーソル1つ(オフライン=P1 / オンライン=自分のスロット)を矢印/WASDで動かす。
    // オフラインの追加プレイヤー(P2〜P4)は未参加ゲームパッドの A で参加・B で取消(ホットシート)。
    // オンラインの追加プレイヤーは右上 NET アイコンから参加。
    private const int MaxPlayers = 4;
    private const float CursorSpeed = 900f;     // px/秒
    private const float NodeGatherRadius = 110f; // ノード集合とみなす半径(px)
    private const float GatherSeconds = 3f;      // 全員集合してから出撃までの猶予

    private Vector2[] cursorPos = new Vector2[MaxPlayers];
    private bool[] joined = new bool[MaxPlayers];
    // オフラインのローカル参加: 各スロットに割り当てたゲームパッド(スロット0=P1はキーボード操作なので未使用)
    private readonly Gamepad[] slotPad = new Gamepad[MaxPlayers];
    // オフラインはホットシート(P1がナビ)なのでカーソルはP1のみ描画する
    private static readonly bool[] LeaderOnlyCursorMask = { true, false, false, false };
    private int activePlayer = 0;                // 直近に操作したプレイヤー
    private int gatherNode = -1;
    private float gatherTimer = 0f;
    private bool launching = false;

    // オンライン・ロビー UI(右上 NET アイコン → 部屋を建てる/入る)
    private NetworkLobbyPanel netPanel;

    private void Start()
    {
        // GameSceneManager がなければ生成
        if (GameSceneManager.Instance == null)
        {
            var go = new GameObject("GameSceneManager");
            go.AddComponent<GameSceneManager>();
        }

        // RunProgress がなければシーン内から探すか生成
        if (runProgress == null)
        {
            runProgress = FindObjectOfType<RunProgress>();
        }

        var map = (runProgress != null && runProgress.worldMap != null) ? runProgress.worldMap : worldMap;
        if (map == null)
        {
            Debug.LogWarning("[WorldMapSceneController] WorldMapData が未設定です。");
            return;
        }

        SpawnView(map);

        // ロビー初期化。カーソルは画面中央付近に置いて即操作可能にする。
        // ローカルは P1 のみ(数字キーでの多人数操作は廃止)。追加プレイヤーはオンライン参加で増える。
        System.Array.Clear(joined, 0, joined.Length);
        for (int p = 0; p < MaxPlayers; p++)
            cursorPos[p] = new Vector2(Screen.width * (0.30f + 0.13f * p), Screen.height * 0.55f);
        joined[0] = true;            // ローカル操作者 = P1
        cursorPos[0] = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        activePlayer = 0;
        gatherNode = -1;
        gatherTimer = 0f;
        launching = false;

        // 前回の募集情報をクリア(このシーンで募集し直す)
        if (GameSceneManager.Instance != null)
            System.Array.Clear(GameSceneManager.Instance.joinedPlayers, 0, 4);

        // オンライン・ロビー UI(NET アイコン)を用意
        if (netPanel == null)
            netPanel = gameObject.AddComponent<NetworkLobbyPanel>();
    }

    private void Update()
    {
        if (view == null || view.IsLocked || launching) return;

        // オンライン・ロビーが稼働中なら、参加状況(スロット占有)を募集枠へ反映する(参加はネットが管理)。
        bool netActive = NetworkLobby.Available && NetworkLobby.Instance.Status == NetLobbyStatus.Active;
        if (netActive) MirrorNetRoster();

        // ロビーパネルを開いている間はマップ操作を停止(コード入力等と競合させない)。
        if (netPanel != null && netPanel.IsOpen)
        {
            view.SetPlayerCursors(cursorPos, joined, activePlayer);
            return;
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        // --- カーソル移動: 操作するのは「自分のカーソル」1つだけ。
        //     オフライン=P1、オンライン=自分のスロット。数字キーによる多人数切替は廃止。 ---
        Vector2 dir = Vector2.zero;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir.x -= 1f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir.x += 1f;
        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dir.y += 1f;
        if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dir.y -= 1f;
        if (dir.sqrMagnitude > 1f) dir = dir.normalized;

        int localSlot = netActive ? LocalNetSlot() : 0; // オフラインは常に P1
        activePlayer = (localSlot >= 0) ? localSlot : 0;

        if (dir != Vector2.zero && localSlot >= 0 && localSlot < MaxPlayers && joined[localSlot])
        {
            // 画面座標は y-down。上入力(dir.y>0)で上へ動かすため y は減算。
            cursorPos[localSlot].x = Mathf.Clamp(cursorPos[localSlot].x + dir.x * CursorSpeed * Time.deltaTime, 0f, Screen.width);
            cursorPos[localSlot].y = Mathf.Clamp(cursorPos[localSlot].y - dir.y * CursorSpeed * Time.deltaTime, 0f, Screen.height);
        }

        if (netActive)
        {
            var lobby = NetworkLobby.Instance;

            // 自分のカーソルを画面正規化(0..1)で送信(解像度が違っても位置が合うように)
            if (localSlot >= 0 && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 norm = new Vector2(cursorPos[localSlot].x / Screen.width,
                                           cursorPos[localSlot].y / Screen.height);
                lobby.SetLocalCursor(norm);
            }

            // 他プレイヤーのカーソルは受信した正規化位置から復元(自分以外)
            var cn = lobby.CursorNorm;
            for (int p = 0; p < MaxPlayers; p++)
            {
                if (p == localSlot || !joined[p] || cn == null || p >= cn.Length) continue;
                cursorPos[p] = new Vector2(cn[p].x * Screen.width, cn[p].y * Screen.height);
            }

            // オンラインの同期出撃(全員集合→遷移)は未実装。集合カウントダウンは行わない。
            // 参加者全員のカーソルをリアルタイム表示する。
            view.SetGather(-1, 0f);
            view.SetPlayerCursors(cursorPos, joined, joined, activePlayer);
        }
        else
        {
            // オフラインのローカル参加(未参加ゲームパッドの A で P2〜P4 参加 / B で取消)
            HandleOfflineLocalJoin();
            // --- 集合判定: 参加者全員が同じノードの近くにいたらカウントダウン ---
            UpdateGather(Time.deltaTime);
            // 追加プレイヤーのカーソルは描かず、リーダー(P1)のカーソルだけ表示(ホットシート)
            view.SetPlayerCursors(cursorPos, joined, LeaderOnlyCursorMask, activePlayer);
        }

        // --- キャンセル(タイトルへ戻る) ---
        if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)
            GameSceneManager.Instance.GoToTitle();
    }

    /// <summary>
    /// オフラインのローカル参加管理。未参加のゲームパッドが A(南ボタン)を押すと次の空きスロット(P2〜P4)に参加、
    /// B(東ボタン)で取消(P1=スロット0 は常時参加で取消不可)。ホットシートのため追加プレイヤーのカーソルは
    /// リーダー(P1)に追従させ、集合判定はリーダーの位置で成立させる。
    /// </summary>
    private void HandleOfflineLocalJoin()
    {
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            var pad = pads[i];
            int slot = SlotOfPad(pad);
            if (pad.buttonSouth.wasPressedThisFrame && slot < 0)
            {
                int free = NextFreeSlot();
                if (free >= 1) { joined[free] = true; slotPad[free] = pad; }
            }
            else if (pad.buttonEast.wasPressedThisFrame && slot >= 1)
            {
                joined[slot] = false;
                slotPad[slot] = null;
            }
        }
        // 追加プレイヤーのカーソルはリーダー(P1)に追従させ、集合判定(全員が同一ノード)を成立させる
        for (int p = 1; p < MaxPlayers; p++)
            if (joined[p]) cursorPos[p] = cursorPos[0];
    }

    /// <summary>ゲームパッドが割り当てられている参加スロット(1〜3)。未割当は -1。</summary>
    private int SlotOfPad(Gamepad pad)
    {
        for (int s = 1; s < MaxPlayers; s++) if (slotPad[s] == pad) return s;
        return -1;
    }

    /// <summary>空いている追加スロット(1〜3)。満席は -1。</summary>
    private int NextFreeSlot()
    {
        for (int s = 1; s < MaxPlayers; s++) if (!joined[s]) return s;
        return -1;
    }

    private static int LocalNetSlot()
        => NetworkLobby.Available ? NetworkLobby.Instance.LocalSlot : -1;

    /// <summary>オンライン・ロビーの参加状況(スロット占有)を募集枠へ反映する。</summary>
    private void MirrorNetRoster()
    {
        var lobby = NetworkLobby.Instance;
        var occ = lobby.SlotOccupied;
        for (int p = 0; p < MaxPlayers; p++)
            joined[p] = (occ != null && p < occ.Length && occ[p]);
        int local = lobby.LocalSlot;
        if (local >= 0 && local < MaxPlayers) activePlayer = local;
    }

    /// <summary>参加者全員が同一ノードの近くに集まっていれば時間を貯め、規定時間で出撃。</summary>
    private void UpdateGather(float dt)
    {
        int common = -2; // -2=未判定, -1=不成立
        bool anyJoined = false;

        for (int p = 0; p < MaxPlayers; p++)
        {
            if (!joined[p]) continue;
            anyJoined = true;
            int n = view.NearestNodeWithin(cursorPos[p], NodeGatherRadius);
            if (n < 0) { common = -1; break; }
            if (common == -2) common = n;
            else if (common != n) { common = -1; break; }
        }

        if (anyJoined && common >= 0)
        {
            if (gatherNode != common) { gatherNode = common; gatherTimer = 0f; }
            gatherTimer += dt;
            view.SetFocusNode(common);
            view.SetGather(common, gatherTimer / GatherSeconds);

            if (gatherTimer >= GatherSeconds)
                Launch(common);
        }
        else
        {
            gatherNode = -1;
            gatherTimer = 0f;
            view.SetGather(-1, 0f);
        }
    }

    private void Launch(int nodeIndex)
    {
        var stage = view.StageAt(nodeIndex);
        if (stage == null)
        {
            Debug.LogError($"[WorldMapSceneController] ノード {nodeIndex} にステージが割り当てられていません。出撃を中止します。");
            gatherNode = -1;
            gatherTimer = 0f;
            return;
        }
        launching = true;
        Debug.Log($"[WorldMapSceneController] 集合完了 → 出撃: {stage.displayName} (参加 {CountJoined()} 人)");

        // 募集結果を引き継ぐ
        if (GameSceneManager.Instance != null)
        {
            for (int p = 0; p < 4; p++)
                GameSceneManager.Instance.joinedPlayers[p] = joined[p];
        }

        OnStageSelected(stage);
    }

    private int CountJoined()
    {
        int c = 0;
        for (int p = 0; p < MaxPlayers; p++) if (joined[p]) c++;
        return c;
    }

    private void SpawnView(WorldMapData map)
    {
        if (view != null)
        {
            view.StageSelected -= OnStageSelected;
            Destroy(view.gameObject);
        }

        var go = new GameObject("WorldMapView");
        view = go.AddComponent<WorldMapView>();
        view.StageSelected += OnStageSelected;
        view.Build(map, runProgress);
    }

    private void OnStageSelected(StageData stage)
    {
        Debug.Log($"[WorldMapSceneController] ステージ選択: {stage.displayName}");

        // RunProgress に記録
        if (runProgress != null)
        {
            runProgress.BeginStage(stage);
        }

        // 選択されたステージをGameSceneManagerに保存
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.selectedStage = stage;
        }

        // キャラクター選択画面へ遷移
        GameSceneManager.Instance.GoToCharacterSelect();
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            view.StageSelected -= OnStageSelected;
        }
    }
}
