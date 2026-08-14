// Unity Netcode for GameObjects + Unity Relay(UGS) による実ロビー実装。
//
// このファイルは丸ごと UGS_NETCODE ガード内。以下が揃うまではコンパイル対象外(＝プロジェクトは壊れない):
//   1) パッケージ導入(Packages/manifest.json に追加済み。Editor 起動で解決):
//        com.unity.netcode.gameobjects / com.unity.services.core /
//        com.unity.services.authentication / com.unity.services.relay
//   2) Scripting Define Symbols に  UGS_NETCODE  を追加
//   3) UGS プロジェクト連携(Project Settings > Services)＋ Relay 有効化
// 詳細手順: Docs/Design/Networking_Design.md
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
    private readonly List<ulong> _ordered = new List<ulong>(); // サーバ側の参加順(host が先頭)
    private bool _hooked;
    private bool _handlersRegistered;

    private Vector2 _localNorm;
    private bool _hasLocalNorm;
    private float _sendAccum;

    public void Tick() { }

    // ---- 公開操作 ----

    public async void HostRoom(int maxPlayers)
    {
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
            if (!_ordered.Contains(_nm.LocalClientId)) _ordered.Add(_nm.LocalClientId);
            BroadcastRoster();
            SetActive();
        }
        catch (Exception e) { Fail("部屋の作成に失敗", e); }
    }

    public async void JoinRoom(string code)
    {
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
                _nm.Shutdown();
        }
        catch (Exception e) { Debug.LogWarning("[NetcodeRelayLobby] Shutdown 中に例外: " + e.Message); }

        _ordered.Clear();
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
        int count = Mathf.Min(_ordered.Count, 4);
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
        int slot = _ordered.IndexOf(senderId);
        if (slot >= 0 && slot < 4) CursorNorm[slot] = v;
    }

    private void OnCursors(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        count = Mathf.Clamp(count, 0, 4);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out Vector2 v);
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
            if (!_ordered.Contains(clientId)) _ordered.Add(clientId);
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
            _ordered.Remove(clientId);
            BroadcastRoster();
        }
        else if (clientId == _nm.LocalClientId)
        {
            LastError = "サーバから切断されました。";
            Status = NetLobbyStatus.Error;
            Raise();
        }
    }

    // ---- ロスター(参加順)の配信・適用 ----

    private void BroadcastRoster()
    {
        if (_nm == null || !_nm.IsServer || _nm.CustomMessagingManager == null) return;

        int count = _ordered.Count;
        var writer = new FastBufferWriter(sizeof(int) + count * sizeof(ulong), Allocator.Temp);
        using (writer)
        {
            writer.WriteValueSafe(count);
            for (int i = 0; i < count; i++) writer.WriteValueSafe(_ordered[i]);
            _nm.CustomMessagingManager.SendNamedMessageToAll(RosterMsg, writer, NetworkDelivery.ReliableSequenced);
        }
        ApplyRoster(_ordered.ToArray()); // サーバ自身にも反映
    }

    private void OnRosterMessage(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        count = Mathf.Clamp(count, 0, 32);
        var ids = new ulong[count];
        for (int i = 0; i < count; i++) reader.ReadValueSafe(out ids[i]);
        ApplyRoster(ids);
        if (Status == NetLobbyStatus.Connecting) SetActive();
    }

    private void ApplyRoster(ulong[] ordered)
    {
        Array.Clear(SlotOccupied, 0, SlotOccupied.Length);
        int n = Mathf.Min(ordered.Length, 4);
        for (int i = 0; i < n; i++) SlotOccupied[i] = true;
        ConnectedCount = ordered.Length;

        LocalSlot = -1;
        if (_nm != null)
        {
            int idx = Array.IndexOf(ordered, _nm.LocalClientId);
            LocalSlot = (idx >= 0 && idx < 4) ? idx : -1;
        }
        Raise();
    }

    // ---- 状態遷移ヘルパ ----

    private void BeginConnecting(NetLobbyRole role)
    {
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
