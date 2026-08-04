using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 回避フェーズのオーケストレーター。
/// 各種マネージャー（Visuals, Player, Enemy, Health, Bomb）を統括してフェーズ進行を管理。
/// </summary>
public class DodgePhase : MonoBehaviour, IBattlePhase
{
    [Header("コンポーネント")]
    [SerializeField] private DodgeVisuals visuals;
    [SerializeField] private DodgePlayerManager playerManager;
    [SerializeField] private DodgeEnemyManager enemyManager;
    [SerializeField] private DodgeHealthManager healthManager;
    [SerializeField] private DodgeBombManager bombManager;

    [Header("依存参照")]
    [SerializeField] private BattleStateMachine stateMachine;
    [SerializeField] private GameTuning tuning;
    [SerializeField] private DanmakuFrameView danmakuFrameView;
    [SerializeField] private BattleEffectProcessor effectProcessor;
    [SerializeField] private CardSelectPhase cardSelectPhase;
    [SerializeField] private ActionCommandPhase actionCommandPhase;

    private bool isActive = false;
    private Coroutine currentRoutine;

    public bool BlocksDebugAdvance => true;

    private void Awake()
    {
        // マネージャーがアタッチされていなければ追加
        if (visuals == null) visuals = gameObject.GetComponent<DodgeVisuals>() ?? gameObject.AddComponent<DodgeVisuals>();
        if (playerManager == null) playerManager = gameObject.GetComponent<DodgePlayerManager>() ?? gameObject.AddComponent<DodgePlayerManager>();
        if (enemyManager == null) enemyManager = gameObject.GetComponent<DodgeEnemyManager>() ?? gameObject.AddComponent<DodgeEnemyManager>();
        if (healthManager == null) healthManager = gameObject.GetComponent<DodgeHealthManager>() ?? gameObject.AddComponent<DodgeHealthManager>();
        if (bombManager == null) bombManager = gameObject.GetComponent<DodgeBombManager>() ?? gameObject.AddComponent<DodgeBombManager>();

        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (danmakuFrameView == null) danmakuFrameView = FindObjectOfType<DanmakuFrameView>();
        if (effectProcessor == null) effectProcessor = FindObjectOfType<BattleEffectProcessor>();
        if (cardSelectPhase == null) cardSelectPhase = FindObjectOfType<CardSelectPhase>();
        if (actionCommandPhase == null) actionCommandPhase = FindObjectOfType<ActionCommandPhase>();

        ApplyTuning();
    }

    private void ApplyTuning()
    {
        if (tuning == null) return;
        playerManager.playerTurnDuration = tuning.playerTurnDuration;
        // 色は tuning にあれば採用、無ければ既定色を維持(白フォールバック防止)
        var cols = tuning.ResolvePlayerColors();
        if (cols != null && cols.Length > 0) playerManager.playerColors = cols;
        playerManager.playerCallouts = tuning.playerCallouts;
        bombManager.playerColors = playerManager.playerColors;
    }

    public void Enter()
    {
        enabled = true;
        isActive = true;

        // HPバーを表示
        var hpGauge = FindObjectOfType<SharedHpGaugeUI>();
        if (hpGauge != null) hpGauge.SetVisible(true);

        // 初期化と参照解決
        var enemiesRoot = GameObject.Find("Enemies")?.transform;
        var centralField = GameObject.Find("Field");

        // Fieldが存在しない場合は自動生成
        if (centralField == null)
        {
            Debug.Log("[DodgePhase] Field not found, creating default field");
            centralField = new GameObject("Field");

            // カメラの中央に配置
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 camCenter = cam.transform.position;
                camCenter.z = 0f; // Z座標は0にして2D平面上に配置
                centralField.transform.position = camCenter;
            }
            else
            {
                centralField.transform.position = Vector3.zero;
            }

            // スプライトレンダラーを追加
            var sr = centralField.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDefaultFieldSprite();
            // フィールドは最背面。"Field" レイヤーは Enemy/Player より上のため、
            // ここに置くとボス・弾・自機(Default)を覆ってしまう。最背面の Default に置く。
            sr.sortingLayerName = "Default";
            sr.sortingOrder = -100;
            sr.color = new Color(0.2f, 0.2f, 0.3f, 1f);
        }

        // プレイヤーの並びは CardSelectPhase が Inspector で持つ P1..P4 順の配列を使う
        // (手番インデックスとキャラを一致させるため。順不同なタグ検索フォールバックは持たない)。
        Transform[] players = new Transform[0];
        if (cardSelectPhase != null && cardSelectPhase.PlayerTransforms != null && cardSelectPhase.PlayerTransforms.Length > 0)
        {
            players = cardSelectPhase.PlayerTransforms;
        }
        else
        {
            Debug.LogError("[DodgePhase] CardSelectPhase.playerTransforms が取得できません。プレイヤーキャラの左右演出は行われません。");
        }

        visuals.Initialize(enemiesRoot, centralField, players);

        // フィールドは生成時点では隠し、PlayFieldAppear で初めて出す。
        // (生成直後に全画面級の暗い矩形が表示され、その後さらに拡大表示されて二重に見えるのを防ぐ)
        if (centralField != null) centralField.SetActive(false);

        // 敵の個別取得
        if (enemiesRoot == null)
        {
            try
            {
                var enemyGOs = GameObject.FindGameObjectsWithTag("Enemy");
                var list = new List<Transform>();
                var pos = new List<Vector3>();
                foreach (var eg in enemyGOs) { list.Add(eg.transform); pos.Add(eg.transform.position); }
                visuals.SetIndividualEnemies(list, pos);
            }
            catch (UnityException)
            {
                // Enemyタグが定義されていない、またはEnemyオブジェクトが存在しない場合
                Debug.LogWarning("[DodgePhase] Enemy tag not found or no enemies in scene");
            }
        }

        // イベント購読
        playerManager.RoundCompleted += OnRoundCompleted;
        playerManager.PlayerHit += OnPlayerHit;
        playerManager.ActivePlayerSwitched += OnActivePlayerSwitched;
        healthManager.HpDepleted += OnHpDepleted;

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(EnterSequence());
    }

    public void Exit()
    {
        if (currentRoutine != null) { StopCoroutine(currentRoutine); currentRoutine = null; }
        if (isActive) StartCoroutine(EndDodgeSequence());
        else enabled = false;
    }

    public void Tick()
    {
        if (isActive) bombManager.Tick(playerManager.CurrentActivePlayer);
    }

    private IEnumerator EnterSequence()
    {
        // 1. 敵の上昇
        yield return visuals.PlayEnemyRise();

        // 2. プレイヤー退避（一度下へ消える）
        yield return visuals.PlayPlayersExitDown();

        // 2.5 左右から入場して待機(以後は左右にゆらゆら浮遊)
        yield return visuals.PlayPlayersSideEntry();

        // 3. 敵（弾幕用）の出現
        var enemyState = FindObjectOfType<EnemyRuntimeState>();
        yield return enemyManager.SpawnAndEnter(enemyState);

        // 4. 中央フィールド出現
        yield return visuals.PlayFieldAppear();

        // 5. 自機生成とターン開始
        SpriteRenderer fieldSr = null;
        if (visuals.centralField != null)
        {
            fieldSr = visuals.centralField.GetComponentInChildren<SpriteRenderer>();
            if (fieldSr != null)
            {
                playerManager.SpawnPlayerOnField(fieldSr.bounds, true, 1f);
            }
            else
            {
                Debug.LogWarning("[DodgePhase] Field SpriteRenderer not found, using default bounds");
                playerManager.SpawnPlayerOnField(new Bounds(Vector3.zero, new Vector3(10, 10, 1)), true, 1f);
            }
        }
        else
        {
            Debug.LogWarning("[DodgePhase] Central field not found, using default bounds");
            playerManager.SpawnPlayerOnField(new Bounds(Vector3.zero, new Vector3(10, 10, 1)), true, 1f);
        }

        // 6. HP初期化
        healthManager.Initialize(tuning != null ? tuning.maxHp : 5);

        // 弾幕枠は実フィールドに合わせて表示(スクリーン固定サイズではなくフィールドを投影)
        if (danmakuFrameView != null)
        {
            danmakuFrameView.SetField(fieldSr);
            danmakuFrameView.SetVisible(true);
        }
        currentRoutine = null;
    }

    public void EndDodge()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(EndDodgeSequence());
    }

    private IEnumerator EndDodgeSequence()
    {
        isActive = false;

        // ドッジフェーズ終了時の効果処理(ノーヒットボーナス等)
        if (effectProcessor != null && cardSelectPhase != null && actionCommandPhase != null)
        {
            effectProcessor.OnDodgePhaseEnd(cardSelectPhase.Selections, actionCommandPhase.PerPlayerSuccess);
        }

        // 1. 敵（弾幕用）の退場
        yield return enemyManager.ExitUpward();

        // 2. 中央フィールド消失
        yield return visuals.PlayFieldExit();

        // 3. プレイヤー復帰
        yield return visuals.PlayPlayersRestore();

        // 4. 常駐敵の復帰
        yield return visuals.PlayEnemyRestore(0.6f, 8f);

        Cleanup();

        // 勝利判定と遷移
        var run = FindObjectOfType<RunProgress>();
        if (run != null && run.currentStage != null)
        {
            run.OnBossDefeated();
            bool stageDone = run.currentStage == null || run.currentBossIndex >= run.currentStage.BossCount;
            if (stageDone)
            {
                if (GameSceneManager.Instance != null) GameSceneManager.Instance.GoToResult(true);
                yield break;
            }
            else
            {
                // 次のボスへ
                var enemyState = FindObjectOfType<EnemyRuntimeState>();
                if (enemyState != null && run.CurrentBoss != null)
                {
                    enemyState.enemyData = run.CurrentBoss;
                    enemyState.ResetAll();
                }
            }
        }

        if (stateMachine != null) stateMachine.ChangeState(BattleStateMachine.State.CardSelect);
    }

    private void OnRoundCompleted() => EndDodge();

    // 手番交代: 前のキャラは光となって戻り、次のキャラが光になって自機の丸へ入り込む
    private void OnActivePlayerSwitched(int index)
    {
        if (visuals == null) return;
        var cols = (playerManager != null) ? playerManager.playerColors : null;
        Color col = (cols != null && index >= 0 && index < cols.Length) ? cols[index] : Color.white;
        visuals.SetActiveCharacterOnField(index, playerManager.OnFieldPlayer, col);
    }

    private void OnPlayerHit(PlayerOnFieldHittable h)
    {
        // BattleEffectProcessorでダメージ計算
        int baseDamage = 1;
        int actualDamage = (effectProcessor != null) ? effectProcessor.CalculateDamageTaken(baseDamage) : baseDamage;

        if (actualDamage > 0)
        {
            healthManager.TakeDamage(actualDamage);

            // 被弾フラグを設定(ノーヒットボーナス判定用)
            if (effectProcessor != null)
            {
                effectProcessor.WasHitThisTurn = true;
            }
        }
    }

    private void OnHpDepleted()
    {
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.GoToResult(false);
        else enabled = false;
    }

    private void Cleanup()
    {
        playerManager.Cleanup();
        enemyManager.Cleanup();
        healthManager.Cleanup();
        playerManager.RoundCompleted -= OnRoundCompleted;
        playerManager.PlayerHit -= OnPlayerHit;
        playerManager.ActivePlayerSwitched -= OnActivePlayerSwitched;
        healthManager.HpDepleted -= OnHpDepleted;
        if (danmakuFrameView != null) danmakuFrameView.SetVisible(false);
        enabled = false;
    }

    private Sprite CreateDefaultFieldSprite()
    {
        // 10x10のシンプルな四角形スプライトを作成
        int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color fieldColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        Color borderColor = new Color(0.4f, 0.4f, 0.6f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 枠
                if (x < 4 || x >= size - 4 || y < 4 || y >= size - 4)
                {
                    texture.SetPixel(x, y, borderColor);
                }
                else
                {
                    texture.SetPixel(x, y, fieldColor);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 25.6f);
    }
}

