// Unity Netcode for GameObjects + Unity Relay(UGS) による実ロビー実装。
//
// このファイルは丸ごと UGS_NETCODE ガード内。以下が揃うまではコンパイル対象外(＝プロジェクトは壊れない):
//   1) パッケージ導入(Packages/manifest.json に追加済み。Editor 起動で解決):
//        com.unity.netcode.gameobjects / com.unity.services.core /
//        com.unity.services.authentication / com.unity.services.relay
//   2) Scripting Define Symbols に  UGS_NETCODE  を追加
//   3) UGS プロジェクト連携(Project Settings > Services)＋ Relay 有効化
// 詳細手順: docs/Design/Networking_Design.md
#if UGS_NETCODE
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Relay の JoinCode を「ルームコード」として使うロビー。
/// ホスト=1P、参加順に 2P/3P/4P を割り当てる。スロット割当・カーソル位置同期は
/// CustomMessaging で行い、NetworkObject の spawn を不要にしている。
/// </summary>
public sealed class NetcodeRelayLobby : MonoBehaviour, INetworkLobby
{
    private const string RosterMsg = "UTS_LobbyRoster"; // server → all: 参加順(slot 割当)
    private const string CurMsg = "UTS_Cur";            // client → server: 自分のカーソル
    private const string CursMsg = "UTS_Curs";          // server → all: 全スロットのカーソル
    private const string ConnType = "dtls";
    private const float SendInterval = 1f / 30f;        // カーソル送信レート(30Hz)

    public NetLobbyStatus Status { get; private set; } = NetLobbyStatus.Idle;
    public NetLobbyRole Role { get; private set; } = NetLobbyRole.None;
    public string JoinCode { get; private set; } = string.Empty;
    public int LocalSlot { get; private set; } = -1;
    public bool[] SlotOccupied { get; } = new bool[4];
    public int ConnectedCount { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public Vector2[] CursorNorm { get; } = new Vector2[4];

    public event Action Changed;

    private NetworkManager _nm;
    private UnityTransport _utp;
    // サーバ側の座席表: index=スロット(0=1P=ホスト)、値=clientId。抜けた人の席だけ空く(他の人のスロットは動かない)
    private readonly ulong?[] _slots = new ulong?[4];
    private bool _hooked;
    private bool _handlersRegistered;
    private bool _leaving; // 自分から退出中(この間の自分の切断はエラー扱いしない)

    private Vector2 _localNorm;
    private bool _hasLocalNorm;
    private float _sendAccum;

    public void Tick() { }

    // ---- 公開操作 ----

    public async void HostRoom(int maxPlayers)
    {
        if (!CanStartNewSession("部屋を建てる")) return;
        try
        {
            BeginConnecting(NetLobbyRole.Host);
            await EnsureServices();
            EnsureNetworkManager();

            int maxOthers = Mathf.Clamp(maxPlayers, 2, 4) - 1; // Relay の maxConnections は「自分以外」の数
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxOthers);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            // 版差注意: 古い/新しいで RelayServerData の作り方が違う場合は
            //   AllocationUtils.ToRelayServerData(alloc, ConnType) 等に置き換える。
            _utp.SetRelayServerData(new RelayServerData(alloc, ConnType));

            HookCommon();
            if (!_nm.StartHost())
                throw new Exception("NetworkManager.StartHost() が false を返しました(トランスポート設定を確認)。");

            RegisterHandlers();
            AssignSlot(_nm.LocalClientId); // ホストは空いている最初の席=1P
            BroadcastRoster();
            SetActive();
        }
        catch (Exception e) { Fail("部屋の作成に失敗", e); }
    }

    public async void JoinRoom(string code)
    {
        if (!CanStartNewSession("部屋に入る")) return;
        try
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new Exception("参加コードが空です。");

            BeginConnecting(NetLobbyRole.Client);
            JoinCode = code.Trim();
            await EnsureServices();
            EnsureNetworkManager();

            JoinAllocation j = await RelayService.Instance.JoinAllocationAsync(JoinCode);
            _utp.SetRelayServerData(new RelayServerData(j, ConnType));

            HookCommon();
            if (!_nm.StartClient())
                throw new Exception("NetworkManager.StartClient() が false を返しました(コード/接続を確認)。");
            RegisterHandlers(); // StartClient 直後に登録(サーバ配信の取りこぼし防止)
            // Active はロスター受信 or OnClientConnected で確定する
        }
        catch (Exception e) { Fail("部屋への参加に失敗", e); }
    }

    public void Leave()
    {
        try
        {
            if (_nm != null && (_nm.IsListening || _nm.IsClient || _nm.IsServer))
            {
                _leaving = true; // Shutdown は遅延処理。直後に来る自分の切断通知をエラーにしない
                _nm.Shutdown();
            }
        }
        catch (Exception e) { Debug.LogWarning("[NetcodeRelayLobby] Shutdown 中に例外: " + e.Message); }

        Array.Clear(_slots, 0, _slots.Length);
        Array.Clear(SlotOccupied, 0, SlotOccupied.Length);
        Array.Clear(CursorNorm, 0, CursorNorm.Length);
        ConnectedCount = 0;
        LocalSlot = -1;
        JoinCode = string.Empty;
        Role = NetLobbyRole.None;
        LastError = string.Empty;
        Status = NetLobbyStatus.Idle;
        _handlersRegistered = false;
        _hasLocalNorm = false;
        Raise();
    }

    // ---- カーソル同期 ----

    public void SetLocalCursor(Vector2 normalized)
    {
        _localNorm = normalized;
        _hasLocalNorm = true;
        if (LocalSlot >= 0 && LocalSlot < 4) CursorNorm[LocalSlot] = normalized; // 自分は即時反映
    }

    private void Update()
    {
        if (_nm == null || !_hasLocalNorm || Status != NetLobbyStatus.Active) return;
        if (_nm.CustomMessagingManager == null) return;

        _sendAccum += Time.unscaledDeltaTime;
        if (_sendAccum < SendInterval) return;
        _sendAccum = 0f;

        if (_nm.IsServer)
        {
            BroadcastCursors();      // ホスト: 全スロットの最新位置を配信
        }
        else if (_nm.IsClient)
        {
            var w = new FastBufferWriter(16, Allocator.Temp);
            using (w)
            {
                w.WriteValueSafe(_localNorm);
                _nm.CustomMessagingManager.SendNamedMessage(CurMsg, NetworkManager.ServerClientId, w, NetworkDelivery.Unreliable);
            }
        }
    }

    private void BroadcastCursors()
    {
        const int count = 4; // 席ごとに送る(空席は無視される)
        var w = new FastBufferWriter(sizeof(int) + count * 8, Allocator.Temp);
        using (w)
        {
            w.WriteValueSafe(count);
            for (int i = 0; i < count; i++) w.WriteValueSafe(CursorNorm[i]);
            _nm.CustomMessagingManager.SendNamedMessageToAll(CursMsg, w, NetworkDelivery.Unreliable);
        }
    }

    private void OnClientCursor(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out Vector2 v);
        if (!TrySanitizeCursor(ref v)) return; // NaN/∞ など不正な値は受け付けない
        int slot = SlotOf(senderId);
        if (slot >= 0) CursorNorm[slot] = v;
    }

    private void OnCursors(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        count = Mathf.Clamp(count, 0, 4);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out Vector2 v);
            if (!TrySanitizeCursor(ref v)) continue;
            if (i != LocalSlot) CursorNorm[i] = v; // 自分の位置はローカル入力を優先(ジッタ回避)
        }
    }

    // ---- UGS / NetworkManager 準備 ----

    private static async Task EnsureServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void EnsureNetworkManager()
    {
        if (_nm != null) { EnsureTransport(); return; }

        _nm = NetworkManager.Singleton != null ? NetworkManager.Singleton
                                               : gameObject.GetComponent<NetworkManager>();
        if (_nm == null) _nm = gameObject.AddComponent<NetworkManager>();
        EnsureTransport();

        if (_nm.NetworkConfig == null) _nm.NetworkConfig = new NetworkConfig();
        _nm.NetworkConfig.NetworkTransport = _utp;
        _nm.NetworkConfig.ConnectionApproval = false;
        _nm.NetworkConfig.PlayerPrefab = null; // プレイヤーオブジェクトは spawn しない
    }

    private void EnsureTransport()
    {
        var host = _nm != null ? _nm.gameObject : gameObject;
        _utp = host.GetComponent<UnityTransport>();
        if (_utp == null) _utp = host.AddComponent<UnityTransport>();
    }

    private void HookCommon()
    {
        if (_hooked) return;
        _hooked = true;
        _nm.OnClientConnectedCallback += OnClientConnected;
        _nm.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void RegisterHandlers()
    {
        if (_handlersRegistered || _nm == null || _nm.CustomMessagingManager == null) return;
        var cmm = _nm.CustomMessagingManager;
        if (_nm.IsServer)
        {
            cmm.RegisterNamedMessageHandler(CurMsg, OnClientCursor); // クライアントのカーソルを受信
        }
        if (_nm.IsClient && !_nm.IsServer)
        {
            cmm.RegisterNamedMessageHandler(RosterMsg, OnRosterMessage); // スロット割当を受信
            cmm.RegisterNamedMessageHandler(CursMsg, OnCursors);        // 全員のカーソルを受信
        }
        _handlersRegistered = true;
    }

    // ---- 接続コールバック(サーバ側でスロット順を管理) ----

    private void OnClientConnected(ulong clientId)
    {
        if (_nm.IsServer)
        {
            if (SlotOf(clientId) < 0 && !AssignSlot(clientId))
            {
                Debug.LogWarning($"[NetcodeRelayLobby] 満員のため client {clientId} を切断します(最大4人)。");
                _nm.DisconnectClient(clientId);
                return;
            }
            BroadcastRoster();
        }
        else if (clientId == _nm.LocalClientId)
        {
            RegisterHandlers();
            if (Status == NetLobbyStatus.Connecting) SetActive();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (_nm.IsServer)
        {
            int slot = SlotOf(clientId);
            if (slot >= 0)
            {
                _slots[slot] = null;           // 抜けた人の席だけ空ける
                CursorNorm[slot] = Vector2.zero;
            }
            BroadcastRoster();
        }
        else if (clientId == _nm.LocalClientId)
        {
            if (_leaving) return; // 自分から退出した結果の切断
            LastError = "サーバから切断されました。";
            Status = NetLobbyStatus.Error;
            Raise();
        }
    }

    // ---- ロスター(参加順)の配信・適用 ----

    private void BroadcastRoster()
    {
        if (_nm == null || !_nm.IsServer || _nm.CustomMessagingManager == null) return;

        // 形式: int 席数(4) → 席ごとに [bool 埋まっているか, ulong clientId]
        int count = _slots.Length;
        var writer = new FastBufferWriter(sizeof(int) + count * (sizeof(bool) + sizeof(ulong)), Allocator.Temp);
        using (writer)
        {
            writer.WriteValueSafe(count);
            for (int i = 0; i < count; i++)
            {
                writer.WriteValueSafe(_slots[i].HasValue);
                writer.WriteValueSafe(_slots[i] ?? 0UL);
            }
            _nm.CustomMessagingManager.SendNamedMessageToAll(RosterMsg, writer, NetworkDelivery.ReliableSequenced);
        }
        ApplyRoster(_slots); // サーバ自身にも反映
    }

    private void OnRosterMessage(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        count = Mathf.Clamp(count, 0, 4);
        var seats = new ulong?[4];
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out bool has);
            reader.ReadValueSafe(out ulong id);
            seats[i] = has ? id : (ulong?)null;
        }
        ApplyRoster(seats);
        if (Status == NetLobbyStatus.Connecting) SetActive();
    }

    private void ApplyRoster(ulong?[] seats)
    {
        Array.Clear(SlotOccupied, 0, SlotOccupied.Length);
        ConnectedCount = 0;
        LocalSlot = -1;
        for (int i = 0; i < SlotOccupied.Length && i < seats.Length; i++)
        {
            if (!seats[i].HasValue) { CursorNorm[i] = Vector2.zero; continue; }
            SlotOccupied[i] = true;
            ConnectedCount++;
            if (_nm != null && seats[i].Value == _nm.LocalClientId) LocalSlot = i;
        }
        Raise();
    }

    // ---- 座席表ヘルパ(サーバ側) ----

    private int SlotOf(ulong clientId)
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i].HasValue && _slots[i].Value == clientId) return i;
        return -1;
    }

    /// <summary>空いている最初の席に座らせる。満席なら false。</summary>
    private bool AssignSlot(ulong clientId)
    {
        if (SlotOf(clientId) >= 0) return true;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].HasValue) continue;
            _slots[i] = clientId;
            return true;
        }
        return false;
    }

    /// <summary>ネットから届いたカーソル座標を検証する。有限値でなければ捨て、0..1 に収める。</summary>
    private static bool TrySanitizeCursor(ref Vector2 v)
    {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y)) return false;
        v = new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
        return true;
    }

    // ---- 状態遷移ヘルパ ----

    /// <summary>
    /// 新しく部屋を建てる/入る操作を始めてよいか。接続中・参加中や、前の接続の終了処理中は始めない。
    /// (始めると Relay の割当や JoinCode が上書きされ、今の接続が壊れるため)
    /// </summary>
    private bool CanStartNewSession(string what)
    {
        string reason = null;
        if (Status == NetLobbyStatus.Connecting) reason = "接続処理中です";
        else if (Status == NetLobbyStatus.Active) reason = "すでに部屋に参加しています。先に「退出」してください";
        else if (_nm != null && _nm.ShutdownInProgress) reason = "前の接続の終了処理中です。少し待ってからやり直してください";
        else if (_nm != null && _nm.IsListening) reason = "前の接続がまだ残っています。「退出」してからやり直してください";
        if (reason == null) return true;

        LastError = $"{what}ことができません: {reason}。";
        Debug.LogError($"[NetcodeRelayLobby] {LastError} (Status={Status}, Role={Role})");
        Raise();
        return false;
    }

    private void BeginConnecting(NetLobbyRole role)
    {
        _leaving = false;
        Role = role;
        Status = NetLobbyStatus.Connecting;
        LastError = string.Empty;
        Raise();
    }

    private void SetActive()
    {
        Status = NetLobbyStatus.Active;
        Raise();
    }

    private void Fail(string what, Exception e)
    {
        Status = NetLobbyStatus.Error;
        LastError = $"{what}: {e.Message}";
        Debug.LogError($"[NetcodeRelayLobby] {LastError}\n{e}");
        Raise();
    }

    private void Raise() => Changed?.Invoke();

    private void OnDestroy()
    {
        if (_nm != null && _hooked)
        {
            _nm.OnClientConnectedCallback -= OnClientConnected;
            _nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
#endif
