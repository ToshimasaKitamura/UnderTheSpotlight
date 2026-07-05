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
    public Color[] playerColors;
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

    public int CurrentActivePlayer => TurnManager != null ? TurnManager.CurrentActivePlayer : -1;

    public void SpawnPlayerOnField(Bounds bounds, bool enableBounds, float bottomMargin)
    {
        Cleanup();
        if (playerOnFieldPrefab == null) return;

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
        TurnManager.playerCount = (playerColors != null) ? playerColors.Length : 4;
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
