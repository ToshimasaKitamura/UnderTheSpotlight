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
    [SerializeField] private BattleFrameUI frameUI;
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
        if (frameUI == null) frameUI = FindObjectOfType<BattleFrameUI>();
        if (effectProcessor == null) effectProcessor = FindObjectOfType<BattleEffectProcessor>();
        if (cardSelectPhase == null) cardSelectPhase = FindObjectOfType<CardSelectPhase>();
        if (actionCommandPhase == null) actionCommandPhase = FindObjectOfType<ActionCommandPhase>();

        ApplyTuning();
    }

    private void ApplyTuning()
    {
        if (tuning == null) return;
        enemyManager.tuning = tuning;
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
        // 0. 中央のボス枠(BattleFrameUI.bossFrame)を隠す。弾幕中はボスがフィールド上に出るため、
        //    OMORI風の中央枠が残ると「フィールド内の謎のフレーム」として見えてしまう。
        if (frameUI == null) frameUI = FindObjectOfType<BattleFrameUI>();
        if (frameUI != null)
        {
            if (frameUI.bossFrame != null) frameUI.bossFrame.gameObject.SetActive(false);
            // ボス直下の共有HPゲージ(敵HPとして見える方)も弾幕中は隠す
            if (frameUI.sharedHpGauge != null) frameUI.sharedHpGauge.gameObject.SetActive(false);
        }

        // 1. 敵の上昇
        yield return visuals.PlayEnemyRise();

        // 2. プレイヤーの縮小+フェード退場に合わせて、四隅の枠も同じ時間で一緒にフェードアウトさせる。
        //    (枠だけ即時に消えると不自然なため、プレイヤーと同期させる)
        //    frameUI は BossIntroPhase が実行時に生成するため Awake で取り逃すことがある → 遅延取得。
        if (frameUI == null) frameUI = FindObjectOfType<BattleFrameUI>();
        Coroutine frameFade = null;
        if (frameUI != null) frameFade = StartCoroutine(frameUI.HideAllyFramesAnimated(visuals.playerExitDuration));
        else Debug.LogWarning("[DodgePhase] BattleFrameUI が見つからず、弾幕中に四隅枠を隠せません。");

        yield return visuals.PlayPlayersExitDown();
        if (frameFade != null) yield return frameFade; // 枠フェード完了を待ってから次へ

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

        // 弾のクリップ範囲をフィールド境界に合わせる。これが無いと弾が枠外へ突き抜けてから消える。
        // ボスは中央フィールド内(y=3 付近)から撃つため、境界=フィールドで弾は枠際で消える。
        if (fieldSr != null)
        {
            var dm = (enemyManager != null) ? enemyManager.danmakuManager : null;
            if (dm == null) dm = FindObjectOfType<DanmakuManager>();
            if (dm != null)
            {
                dm.fieldBounds = fieldSr.bounds;
                dm.useFieldBounds = true;
            }
            else Debug.LogWarning("[DodgePhase] DanmakuManager が見つからず、弾のフィールドクリップを設定できません。");
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

        // 0. 先に自機と弾を消す。
        //    フィールド/枠(danmakuFrameView)を畳む前に撤去しないと、「枠は消えたのに自機・弾だけ残る」ように見えるため。
        var dm = (enemyManager != null) ? enemyManager.danmakuManager : null;
        if (dm == null) dm = FindObjectOfType<DanmakuManager>();
        if (dm != null) dm.ClearAllBullets();
        if (playerManager != null) playerManager.Cleanup();   // 自機(丸)・照準リング・支援オブジェクトを即撤去

        // ドッジフェーズ終了時の効果処理(ノーヒットボーナス等)
        if (effectProcessor != null && cardSelectPhase != null && actionCommandPhase != null)
        {
            effectProcessor.OnDodgePhaseEnd(cardSelectPhase.Selections, actionCommandPhase.PerPlayerSuccess);
        }

        // 1. 敵（弾幕用）の退場
        yield return enemyManager.ExitUpward();

        // 2. 中央フィールド消失
        yield return visuals.PlayFieldExit();

        // 3. 先に四隅の味方枠とボス枠を復活させる(枠が戻ってからキャラを出す)
        if (frameUI != null)
        {
            frameUI.ShowAllyFramesOnly();
            if (frameUI.bossFrame != null) frameUI.bossFrame.gameObject.SetActive(true);
            if (frameUI.sharedHpGauge != null) frameUI.sharedHpGauge.gameObject.SetActive(true);
        }

        // 4. 枠が出そろってからプレイヤーを四隅へ戻す
        yield return visuals.PlayPlayersRestore();

        // 5. 常駐敵の復帰
        yield return visuals.PlayEnemyRestore(0.6f, 8f);

        Cleanup();

        // 弾幕を生き延びたら次のラウンド(カード選択)へ戻る。
        // ボス撃破(=全部位破壊)の判定と連戦の進行は BattleResultPhase が担当する。
        // ここで撃破扱いにすると「弾幕を1回終える＝即勝利」になり、ボスHPが機能しなくなる。
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

    private bool deathSequenceStarted;

    private void OnHpDepleted()
    {
        if (deathSequenceStarted) return;
        deathSequenceStarted = true;
        StartCoroutine(PlayerDeathSequence());
    }

    /// <summary>
    /// HP0時のゲームオーバー演出:
    /// 手番交代を止める → ヒットストップ(時間停止)しながらヒット箇所へズームイン →
    /// 自機飛散＋画面揺れ → 画面をスッと引く(ズームアウト) → リザルト(敗北)。
    /// </summary>
    private IEnumerator PlayerDeathSequence()
    {
        isActive = false; // 以降の手番/被弾処理を止める

        // 手番交代を止める(この演出中に次のプレイヤーへ回さない)
        if (playerManager != null && playerManager.TurnManager != null)
            playerManager.TurnManager.StopTurns();

        // 弾幕を止める
        if (enemyManager != null && enemyManager.DanmakuPattern != null)
            enemyManager.DanmakuPattern.StopPattern();

        var fieldPlayer = playerManager != null ? playerManager.OnFieldPlayer : null;
        Vector3 deathPos = fieldPlayer != null ? fieldPlayer.position
            : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        Color col = GetActivePlayerColor();

        Camera cam = Camera.main;
        float baseSize = cam != null ? cam.orthographicSize : 5f;
        Vector3 basePos = cam != null ? cam.transform.position : Vector3.zero;

        // 1. ヒットストップ(時間停止)。止めたままヒット箇所へズームイン(unscaledで動かす)。
        Time.timeScale = 0f;
        if (cam != null)
        {
            Vector3 zoomPos = new Vector3(deathPos.x, deathPos.y, basePos.z);
            yield return StartCoroutine(ZoomCamera(cam, baseSize * 0.4f, zoomPos, 0.28f, true));
        }
        // 止めた瞬間をしっかり見せる(ヒットストップ長め)
        yield return new WaitForSecondsRealtime(0.55f);

        // 2. 自機を隠して破片を飛散、残弾も消す。時間を再開。
        if (fieldPlayer != null) fieldPlayer.gameObject.SetActive(false);
        SpawnDeathShatter(deathPos, col);

        var dm = (enemyManager != null) ? enemyManager.danmakuManager : null;
        if (dm == null) dm = FindObjectOfType<DanmakuManager>();
        if (dm != null) dm.ClearAllBullets();

        Time.timeScale = 1f;

        // 3. 破片が飛ぶ瞬間に短く画面揺れ(ズーム位置のまま)
        yield return StartCoroutine(ScreenShake(0.28f, 0.22f));

        // 4. 画面をスッと引く(元のサイズ・位置へズームアウト)
        if (cam != null)
            yield return StartCoroutine(ZoomCamera(cam, baseSize, basePos, 0.55f, false));

        yield return new WaitForSeconds(0.1f);

        // 5. ゲームオーバーへ
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.GoToResult(false);
        else enabled = false;
    }

    /// <summary>オルソカメラをズーム(サイズ+位置)補間する。unscaled=true でヒットストップ中も動く。</summary>
    private IEnumerator ZoomCamera(Camera cam, float targetSize, Vector3 targetPos, float duration, bool unscaled)
    {
        float startSize = cam.orthographicSize;
        Vector3 startPos = cam.transform.position;
        float t = 0f;
        while (t < duration)
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, p);
            cam.transform.position = Vector3.Lerp(startPos, targetPos, p);
            yield return null;
        }
        cam.orthographicSize = targetSize;
        cam.transform.position = targetPos;
    }

    private Color GetActivePlayerColor()
    {
        var cols = playerManager != null ? playerManager.playerColors : null;
        int idx = playerManager != null ? playerManager.CurrentActivePlayer : -1;
        if (cols != null && idx >= 0 && idx < cols.Length) return cols[idx];
        return Color.white;
    }

    /// <summary>自機(球)が飛び散る破片エフェクト。</summary>
    private void SpawnDeathShatter(Vector3 pos, Color color)
    {
        var sprite = GetDotSprite();
        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("DeathFrag");
            go.transform.position = pos;
            float s = UnityEngine.Random.Range(0.06f, 0.15f);
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 60;
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float spd = UnityEngine.Random.Range(2.5f, 6.5f);
            Vector3 vel = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * spd;
            StartCoroutine(AnimateFragment(go, sr, vel));
        }
    }

    private IEnumerator AnimateFragment(GameObject go, SpriteRenderer sr, Vector3 vel)
    {
        const float life = 0.7f;
        float t = 0f;
        Vector3 gravity = new Vector3(0f, -9f, 0f);
        Color baseCol = sr.color;
        while (t < life && go != null)
        {
            t += Time.deltaTime;
            vel += gravity * Time.deltaTime;
            go.transform.position += vel * Time.deltaTime;
            go.transform.Rotate(0f, 0f, 540f * Time.deltaTime);
            var c = baseCol; c.a = 1f - (t / life); sr.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private IEnumerator ScreenShake(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 basePos = cam.transform.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = Mathf.Clamp01(1f - t / duration);
            float ox = UnityEngine.Random.Range(-1f, 1f) * magnitude * damper;
            float oy = UnityEngine.Random.Range(-1f, 1f) * magnitude * damper;
            cam.transform.position = basePos + new Vector3(ox, oy, 0f);
            yield return null;
        }
        cam.transform.position = basePos;
    }

    private static Sprite _dotSprite;
    private static Sprite GetDotSprite()
    {
        if (_dotSprite != null) return _dotSprite;
        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Point;
        var w = new Color32(255, 255, 255, 255);
        tex.SetPixels32(new Color32[] { w, w, w, w });
        tex.Apply();
        _dotSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        _dotSprite.name = "DodgeDeathDot";
        return _dotSprite;
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
        // 10x10のシンプルな四角形スプライト。枠線は描かない(枠は DanmakuFrameView が1本だけ描く)。
        // ここで枠を描くと黒いフィールド内にもう1本の枠が見えて二重になるため、ソリッド塗りにする。
        int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color fieldColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = fieldColor;
        texture.SetPixels(pixels);

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 25.6f);
    }
}

