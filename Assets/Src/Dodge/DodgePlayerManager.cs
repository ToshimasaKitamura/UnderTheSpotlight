using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弾幕回避フェーズにおけるプレイヤーの自機管理・ターン管理を担当。
/// </summary>
public class DodgePlayerManager : MonoBehaviour
{
    [Header("設定")]
    public GameObject playerOnFieldPrefab;
    // 既定のプレイヤーカラー(tuning未設定でも自機が色付くように)
    public Color[] playerColors = new Color[]
    {
        new Color(1f, 0.35f, 0.35f, 1f),   // P1 赤
        new Color(0.35f, 0.6f, 1f, 1f),    // P2 青
        new Color(0.35f, 1f, 0.4f, 1f),    // P3 緑
        new Color(1f, 0.85f, 0.35f, 1f),   // P4 黄
    };
    public float playerTurnDuration = 4f;
    public string[] playerCallouts;

    // 実行時インスタンス
    public PlayerTurnManager TurnManager { get; private set; }
    public PlayerSwitchAnnouncer Announcer { get; private set; }
    public ActivePlayerRing ActivePlayerRing { get; private set; }
    public DebugMultiPlayerInput DebugInput { get; private set; }
    private List<GameObject> playerOnFieldInstances = new List<GameObject>();
    private List<PlayerOnFieldHittable> subscribedHittables = new List<PlayerOnFieldHittable>();

    public event Action RoundCompleted;
    public event Action<PlayerOnFieldHittable> PlayerHit;
    /// <summary>手番プレイヤーが切り替わったとき(index)。演出連携用。</summary>
    public event Action<int> ActivePlayerSwitched;

    public int CurrentActivePlayer => TurnManager != null ? TurnManager.CurrentActivePlayer : -1;

    /// <summary>弾幕フィールド上の自機(丸)の Transform。無ければ null。</summary>
    public Transform OnFieldPlayer =>
        (playerOnFieldInstances != null && playerOnFieldInstances.Count > 0 && playerOnFieldInstances[0] != null)
            ? playerOnFieldInstances[0].transform : null;

    public void SpawnPlayerOnField(Bounds bounds, bool enableBounds, float bottomMargin)
    {
        Cleanup();

        if (playerOnFieldPrefab == null)
        {
            Debug.LogError("[DodgePlayerManager] playerOnFieldPrefab が未設定です。自機(球体)のプレハブを Inspector で設定してください。自機を生成できません。");
            return;
        }

        Vector3 spawnPos = new Vector3(bounds.center.x, bounds.min.y + bottomMargin, 0f);
        var go = Instantiate(playerOnFieldPrefab, spawnPos, Quaternion.identity);
        go.name = "PlayerOnField";

        DebugInput = go.GetComponent<DebugMultiPlayerInput>() ?? go.AddComponent<DebugMultiPlayerInput>();
        var controller = go.GetComponent<PlayerOnFieldController>() ?? go.AddComponent<PlayerOnFieldController>();
        controller.movementBounds = bounds;
        controller.enableBounds = enableBounds;

        var hittable = go.GetComponent<PlayerOnFieldHittable>() ?? go.AddComponent<PlayerOnFieldHittable>();
        hittable.Hit += OnHittableHit;
        subscribedHittables.Add(hittable);
        playerOnFieldInstances.Add(go);

        SpawnSupportObjects(go.transform);
        StartTurnRotation();
    }

    private void SpawnSupportObjects(Transform target)
    {
        var ringObj = new GameObject("ActivePlayerRing");
        ActivePlayerRing = ringObj.AddComponent<ActivePlayerRing>();
        ActivePlayerRing.playerColors = playerColors;
        ActivePlayerRing.target = target;

        var anObj = new GameObject("PlayerSwitchAnnouncer");
        Announcer = anObj.AddComponent<PlayerSwitchAnnouncer>();
        Announcer.playerColors = playerColors;
        if (playerCallouts != null) Announcer.callouts = playerCallouts;
    }

    private void StartTurnRotation()
    {
        var tmObj = new GameObject("PlayerTurnManager");
        TurnManager = tmObj.AddComponent<PlayerTurnManager>();
        // 参加人数(選択パーティ)に合わせる。色配列(最大4)を上限にクランプ。
        int maxColors = (playerColors != null) ? playerColors.Length : 4;
        TurnManager.playerCount = Mathf.Clamp(BattleParty.Count(null), 1, maxColors);
        TurnManager.turnDuration = playerTurnDuration;
        TurnManager.ActivePlayerChanged += OnActivePlayerChanged;
        TurnManager.RoundCompleted += OnRoundCompletedInternal;

        if (DebugInput != null) DebugInput.turnSource = TurnManager;
        TurnManager.StartTurns(0);
    }

    private void OnActivePlayerChanged(int index)
    {
        if (ActivePlayerRing != null) ActivePlayerRing.SetActivePlayer(index);
        if (Announcer != null) Announcer.Announce(index);
        ActivePlayerSwitched?.Invoke(index);
    }

    private void OnRoundCompletedInternal() => RoundCompleted?.Invoke();
    private void OnHittableHit(PlayerOnFieldHittable h) => PlayerHit?.Invoke(h);

    public void Cleanup()
    {
        foreach (var h in subscribedHittables) if (h != null) h.Hit -= OnHittableHit;
        subscribedHittables.Clear();

        if (TurnManager != null)
        {
            TurnManager.ActivePlayerChanged -= OnActivePlayerChanged;
            TurnManager.RoundCompleted -= OnRoundCompletedInternal;
            Destroy(TurnManager.gameObject);
        }
        if (Announcer != null) Destroy(Announcer.gameObject);
        if (ActivePlayerRing != null) Destroy(ActivePlayerRing.gameObject);
        foreach (var go in playerOnFieldInstances) if (go != null) Destroy(go);
        playerOnFieldInstances.Clear();
    }
}
