using System;
using UnityEngine;

/// <summary>
/// オンライン・ロビー(部屋)の抽象。ステージセレクト画面から「部屋を建てる/入る」を行う。
/// 実トランスポート(Unity Netcode + Relay)は <see cref="NetcodeRelayLobby"/> が担うが、
/// パッケージ未導入(UGS_NETCODE 未定義)でもプロジェクトが壊れないよう、ここは常時コンパイルされる。
///
/// 重要(CLAUDE.md 準拠): パッケージ/サービス未整備時は「とりあえず成功」させない。
/// <see cref="UnavailableLobby"/> が明確なエラーメッセージ(<see cref="LastError"/>)を返す。
/// </summary>
public interface INetworkLobby
{
    NetLobbyStatus Status { get; }
    NetLobbyRole Role { get; }

    /// <summary>ホスト時に発行される参加コード(クライアントは接続に使ったコード)。未発行は空。</summary>
    string JoinCode { get; }

    /// <summary>自分のプレイヤースロット。0=1P(ホスト)..3=4P。未確定は -1。</summary>
    int LocalSlot { get; }

    /// <summary>各スロット(0..3 = P1..P4)が埋まっているか。参加順に埋まる。長さ4。</summary>
    bool[] SlotOccupied { get; }

    /// <summary>接続人数(自分を含む)。</summary>
    int ConnectedCount { get; }

    /// <summary>直近のエラーメッセージ(原因が分かる粒度で入れる)。無ければ空。</summary>
    string LastError { get; }

    /// <summary>各スロット(0..3=P1..P4)のカーソル位置。画面正規化座標(0..1, y-down)。長さ4。</summary>
    Vector2[] CursorNorm { get; }

    /// <summary>自分のカーソル位置(画面正規化 0..1, y-down)を通知する。毎フレーム呼んでよい(内部で送信間引き)。</summary>
    void SetLocalCursor(Vector2 normalized);

    /// <summary>状態が変化したとき(接続/人数/スロット/エラー)に発火。UI 再描画用。</summary>
    event Action Changed;

    /// <summary>部屋を建てる。maxPlayers は自分を含む上限(2〜4)。成功で <see cref="JoinCode"/> が埋まる。</summary>
    void HostRoom(int maxPlayers);

    /// <summary>コードを指定して部屋に入る。</summary>
    void JoinRoom(string code);

    /// <summary>退出して切断する。</summary>
    void Leave();

    /// <summary>毎フレーム呼ぶ(状態ポーリングが必要な実装向け)。</summary>
    void Tick();
}

public enum NetLobbyStatus
{
    Idle,        // 未接続
    Connecting,  // 接続処理中(Relay割当/認証/接続)
    Active,      // 接続確立・ロビー稼働中
    Error        // 失敗(LastError 参照)
}

public enum NetLobbyRole { None, Host, Client }

/// <summary>
/// ロビー実装へのアクセス点。UGS_NETCODE が定義されていれば実通信、無ければ
/// <see cref="UnavailableLobby"/> を返す(明確なエラーを出す用)。
/// </summary>
public static class NetworkLobby
{
    private static INetworkLobby _instance;

    /// <summary>実トランスポート(Netcode+Relay)が利用可能にコンパイルされているか。</summary>
    public static bool Available =>
#if UGS_NETCODE
        true;
#else
        false;
#endif

    public static INetworkLobby Instance
    {
        get
        {
            if (_instance == null) _instance = Create();
            return _instance;
        }
    }

    private static INetworkLobby Create()
    {
#if UGS_NETCODE
        var go = new GameObject("NetcodeRelayLobby");
        UnityEngine.Object.DontDestroyOnLoad(go);
        return go.AddComponent<NetcodeRelayLobby>();
#else
        return new UnavailableLobby();
#endif
    }
}

/// <summary>
/// ネットワークパッケージ未導入/未有効化のときの実装。
/// 何もせず、なぜ使えないかを <see cref="LastError"/> に明示する(フォールバックで握りつぶさない)。
/// </summary>
public sealed class UnavailableLobby : INetworkLobby
{
    private const string Reason =
        "オンライン機能は未有効です。Unity Netcode/Relay パッケージを導入し、" +
        "Scripting Define Symbols に UGS_NETCODE を追加、UGS プロジェクト連携(認証+Relay)を有効にしてください。" +
        "詳細: Docs/Design/Networking_Design.md";

    public NetLobbyStatus Status { get; private set; } = NetLobbyStatus.Idle;
    public NetLobbyRole Role => NetLobbyRole.None;
    public string JoinCode => string.Empty;
    public int LocalSlot => -1;
    public bool[] SlotOccupied { get; } = new bool[4];
    public int ConnectedCount => 0;
    public string LastError { get; private set; } = string.Empty;
    public Vector2[] CursorNorm { get; } = new Vector2[4];

    public event Action Changed;

    public void SetLocalCursor(Vector2 normalized) { }

    private void Fail()
    {
        Status = NetLobbyStatus.Error;
        LastError = Reason;
        Debug.LogError("[NetworkLobby] " + Reason);
        Changed?.Invoke();
    }

    public void HostRoom(int maxPlayers) => Fail();
    public void JoinRoom(string code) => Fail();
    public void Leave() { Status = NetLobbyStatus.Idle; LastError = string.Empty; Changed?.Invoke(); }
    public void Tick() { }
}
