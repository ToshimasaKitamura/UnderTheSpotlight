using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アクションコマンドフェーズ後、ダメージ結果を演出表示するフェーズ。
/// 各プレイヤーの攻撃結果、部位破壊、ボス撃破判定などを表示し、
/// 次のフェーズ(Dodge or 勝利)へ遷移する。
/// </summary>
public class BattleResultPhase : MonoBehaviour, IBattlePhase
{
    [Header("依存(未設定なら自動探索)")]
    [SerializeField] private BattleStateMachine stateMachine;
    [SerializeField] private ActionCommandPhase actionCommandPhase;
    [SerializeField] private CardSelectPhase cardSelectPhase;
    [SerializeField] private EnemyRuntimeState enemyState;
    [SerializeField] private SharedHpController sharedHpController;
    [SerializeField] private GameTuning tuning;

    [Header("表示設定")]
    [Tooltip("結果表示の各項目の表示間隔(秒)")]
    public float itemDisplayInterval = 0.3f;
    [Tooltip("結果表示後の待機時間(秒)")]
    public float postResultDelay = 1.0f;
    [Tooltip("部位破壊演出の時間(秒)")]
    public float partDestroyEffectDuration = 0.8f;

    public bool BlocksDebugAdvance => true;

    private Coroutine routine;
    private BattleResultIMGUIHelper guiHelper;
    // 破壊演出で SetActive(false) にした Enemy を、非アクティブでも参照して再表示するためのキャッシュ。
    // (GameObject.Find / FindObjectOfType は非アクティブを見つけられないため)
    private GameObject cachedBossGo;

    // 表示用データ
    private List<ResultEntry> resultEntries = new List<ResultEntry>();
    private List<string> destroyedPartsThisTurn = new List<string>();
    private bool bossDefeated = false;
    private string currentMessage = "";
    private float messageAlpha = 1f;

    public class ResultEntry
    {
        public int playerIndex;
        public string cardName;
        public CardCategory category;
        public int damage;
        public int healAmount;
        public string targetPartName;
        public bool success;
        public Color playerColor;
        public float displayTime;
    }

    void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponentInParent<BattleStateMachine>();
        if (actionCommandPhase == null) actionCommandPhase = FindObjectOfType<ActionCommandPhase>();
        if (cardSelectPhase == null) cardSelectPhase = FindObjectOfType<CardSelectPhase>();
        if (enemyState == null) enemyState = FindObjectOfType<EnemyRuntimeState>();
        if (sharedHpController == null) sharedHpController = FindObjectOfType<SharedHpController>();
    }

    void Start() { enabled = false; }

    public void Enter()
    {
        enabled = true;
        resultEntries.Clear();
        destroyedPartsThisTurn.Clear();
        bossDefeated = false;
        currentMessage = "";
        messageAlpha = 1f;

        // HPバーを非表示
        var hpGauge = FindObjectOfType<SharedHpGaugeUI>();
        if (hpGauge != null) hpGauge.SetVisible(false);

        // IMGUIヘルパーを追加
        guiHelper = gameObject.AddComponent<BattleResultIMGUIHelper>();
        guiHelper.Initialize(this);

        // 部位破壊イベントを購読
        if (enemyState != null)
        {
            enemyState.OnPartDestroyed += OnPartDestroyed;
        }

        Debug.Log("[BattleResultPhase] 開始");
        routine = StartCoroutine(RunResultSequence());
    }

    public void Exit()
    {
        enabled = false;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        StopAllCoroutines();

        // 破壊演出の途中で抜けてもスローのまま固まらないよう必ず戻す
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (enemyState != null)
        {
            enemyState.OnPartDestroyed -= OnPartDestroyed;
        }

        if (guiHelper != null) { Destroy(guiHelper); guiHelper = null; }
        resultEntries.Clear();
        destroyedPartsThisTurn.Clear();
        currentMessage = "";
        Debug.Log("[BattleResultPhase] 終了");
    }

    public void Tick() { /* コルーチン駆動 */ }

    private void OnPartDestroyed(EnemyPart part)
    {
        if (part != null && !string.IsNullOrEmpty(part.displayName))
        {
            destroyedPartsThisTurn.Add(part.displayName);
        }
    }

    private IEnumerator RunResultSequence()
    {
        var selections = cardSelectPhase?.Selections;
        var perPlayerSuccess = actionCommandPhase?.PerPlayerSuccess;
        int playerCount = actionCommandPhase?.GetPlayerCount() ?? 4;
        var playerColors = actionCommandPhase?.GetPlayerColors();

        // 0. ボス撃破済みなら、結果パレードを挟まず即・破壊演出→次ボス登場へ。
        //    (止めの一撃からヒットストップ/撃破演出までの間延びを防ぐ)
        if (enemyState != null && IsAllPartsDestroyed())
        {
            bossDefeated = true;
            yield return StartCoroutine(PlayBossDefeatEffect());
            routine = null;
            yield return StartCoroutine(HandleBossDefeatedRoutine());
            yield break;
        }

        // 1. 各プレイヤーの結果をエントリーに追加
        currentMessage = "RESULT";
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < playerCount; i++)
        {
            var card = (selections != null && i < selections.Count) ? selections[i] : null;
            int successCount = (perPlayerSuccess != null && i < perPlayerSuccess.Count) ? perPlayerSuccess[i] : 0;

            var entry = new ResultEntry
            {
                playerIndex = i,
                cardName = card?.Data?.displayName ?? "---",
                category = card?.Category ?? CardCategory.Attack,
                damage = (card?.Category == CardCategory.Attack && successCount > 0) ? card.EffectivePower * successCount : 0,
                healAmount = (card?.Category == CardCategory.Heal && successCount > 0) ? card.EffectivePower * successCount : 0,
                success = successCount > 0,
                playerColor = (playerColors != null && i < playerColors.Length) ? playerColors[i] : Color.white,
                displayTime = Time.time
            };

            resultEntries.Add(entry);
            yield return new WaitForSeconds(itemDisplayInterval);
        }

        // 2. 部位破壊があれば演出
        if (destroyedPartsThisTurn.Count > 0)
        {
            yield return new WaitForSeconds(0.3f);
            foreach (var partName in destroyedPartsThisTurn)
            {
                currentMessage = $"BREAK! {partName}";
                yield return new WaitForSeconds(partDestroyEffectDuration);
            }
        }

        // 3. ボス撃破判定
        if (enemyState != null && IsAllPartsDestroyed())
        {
            bossDefeated = true;
            // 破壊時演出(ヒットストップ→時間がゆっくり→爆発→ボスが弾けて消える)
            yield return StartCoroutine(PlayBossDefeatEffect());

            // 勝利処理 or 次ボス登場(降下＋画面振動)
            routine = null;
            yield return StartCoroutine(HandleBossDefeatedRoutine());
            yield break;
        }

        // 4. 通常遷移(Dodgeフェーズへ)
        currentMessage = "DODGE START!";
        yield return new WaitForSeconds(postResultDelay);

        routine = null;
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.Dodge);
    }

    private bool IsAllPartsDestroyed()
    {
        if (enemyState == null || enemyState.enemyData == null) return false;
        var parts = enemyState.enemyData.parts;
        if (parts == null || parts.Count == 0) return false;

        foreach (var part in parts)
        {
            if (part == null || string.IsNullOrEmpty(part.id)) continue;
            if (!enemyState.IsDestroyed(part.id)) return false;
        }
        return true;
    }

    private IEnumerator HandleBossDefeatedRoutine()
    {
        var run = FindObjectOfType<RunProgress>();
        if (run != null)
        {
            run.OnBossDefeated();
            bool stageDone = run.currentStage == null || run.currentBossIndex >= run.currentStage.BossCount;
            if (stageDone)
            {
                if (GameSceneManager.Instance != null)
                    GameSceneManager.Instance.GoToResult(true);
                yield break;
            }

            // 次のボスへ
            if (enemyState != null && run.CurrentBoss != null)
            {
                enemyState.enemyData = run.CurrentBoss;
                enemyState.ResetAll();

                // 連戦: カード選択/AC で見えるボス本体(Enemy ルート＋Visual 子の両 SpriteRenderer)を
                // 次ボスへ更新し、破壊演出で消したボスを再表示する。
                // ※ 従来は EnemyOnFieldController.SetUpSprite(ルートのみ) だったため、カード選択が参照する
                //    Visual 子が更新されず「2体目が弾幕にしか出ない」状態だった。
                UpdateFieldBossVisual(run.CurrentBoss);

                // 次ボスの登場演出(画面外上部から降下＋着地で画面振動)
                var intro = FindObjectOfType<BossIntroPhase>();
                if (intro != null)
                {
                    currentMessage = "";
                    yield return StartCoroutine(intro.PlayBossReentry());
                }
            }
        }

        // カード選択フェーズへ戻る
        if (stateMachine != null)
            stateMachine.ChangeState(BattleStateMachine.State.CardSelect);
    }

    /// <summary>
    /// ボス撃破の破壊演出。時間をゆっくりにし、ボスを白く点滅させてから爆発＋弾けて消す。
    /// スロー中でも進むよう全て unscaledDeltaTime で動かし、終了時に timeScale を必ず戻す。
    /// 最後にボスを GameObject ごと非表示(＝消滅)にし、次ボスがいれば UpdateFieldBossVisual が再表示する。
    /// </summary>
    private IEnumerator PlayBossDefeatEffect()
    {
        currentMessage = "BOSS DEFEATED!";

        GameObject enemyGo = GameObject.Find("Enemy");
        if (enemyGo == null)
        {
            var e = FindObjectOfType<EnemyOnFieldController>();
            if (e != null) enemyGo = e.gameObject;
        }
        cachedBossGo = enemyGo;   // 非アクティブ化後も参照できるよう保持
        Camera cam = Camera.main;
        Vector3 camHome = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 bossPos = enemyGo != null ? enemyGo.transform.position : camHome + Vector3.forward * 10f;
        bossPos.z = 0f;
        Vector3 bossScale0 = enemyGo != null ? enemyGo.transform.localScale : Vector3.one;
        var renderers = enemyGo != null ? enemyGo.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];

        // 0. ヒットストップ: 止めの一撃で一瞬完全停止(ボスを白くしてインパクト強調)
        foreach (var sr in renderers) if (sr != null) sr.color = Color.white;
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0.0001f;
        yield return new WaitForSecondsRealtime(0.18f);

        // 1. ため: 軽いスロー＋ボスが白く点滅して小刻みに震える
        Time.timeScale = 0.4f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        float t = 0f, pre = 0.55f;
        while (t < pre)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.PingPong(t * 18f, 1f);
            foreach (var sr in renderers)
                if (sr != null) sr.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.4f, 1f), k);
            if (cam != null) cam.transform.position = camHome + (Vector3)(Random.insideUnitCircle * 0.08f);
            yield return null;
        }

        // 2. 爆発の瞬間: 強いスロー＋破片を飛散
        Time.timeScale = 0.08f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        float radius = Mathf.Max(0.6f, (bossScale0.x + bossScale0.y) * 0.5f);
        GameObject explosion = SpawnExplosion(bossPos, radius);

        // 3. ボスが膨張しながらフェードして消える＋大きめの画面揺れ
        t = 0f; float dur = 0.7f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            if (enemyGo != null) enemyGo.transform.localScale = bossScale0 * (1f + 0.7f * p);
            foreach (var sr in renderers)
                if (sr != null) { Color c = sr.color; c.a = 1f - p; sr.color = c; }
            float shake = 0.35f * (1f - p);
            if (cam != null) cam.transform.position = camHome + (Vector3)(Random.insideUnitCircle * shake);
            yield return null;
        }

        // 4. 後始末: スロー解除・カメラ復帰・スケール復帰。
        //    ボスは GameObject ごと非表示にする(個別 enabled は触らない。
        //    FloatingCharacterMotion が元レンダラーを無効化した状態を壊さないため)。破片も破棄。
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (cam != null) cam.transform.position = camHome;
        foreach (var sr in renderers)
            if (sr != null) sr.color = Color.white;   // 次表示用に色を戻す
        if (enemyGo != null)
        {
            enemyGo.transform.localScale = bossScale0;
            enemyGo.SetActive(false);
        }
        if (explosion != null) Destroy(explosion);

        yield return new WaitForSecondsRealtime(0.4f);
    }

    /// <summary>連戦の次ボス用に、Enemy 配下の全 SpriteRenderer(ルート＋Visual 子)を再表示し新スプライトを適用する。</summary>
    private void UpdateFieldBossVisual(EnemyData boss)
    {
        // 破壊演出で非アクティブ化しているため、キャッシュ参照を最優先(Find/FindObjectOfType は非アクティブ不可)
        GameObject enemyGo = cachedBossGo;
        if (enemyGo == null) enemyGo = GameObject.Find("Enemy");
        if (enemyGo == null)
        {
            var e = FindObjectOfType<EnemyOnFieldController>();
            if (e != null) enemyGo = e.gameObject;
        }
        if (enemyGo == null)
        {
            Debug.LogWarning("[BattleResultPhase] Enemy オブジェクトが見つからず、次ボスの見た目を更新できません。");
            return;
        }
        Sprite sprite = (boss != null && !string.IsNullOrEmpty(boss.bossSpriteName))
            ? BossSpriteResolver.Load(boss.bossSpriteName) : null;
        // 実際に見えているボス = FloatingCharacterMotion が作る浮遊用 "SpriteHolder"(ルートのコピー)。
        // ルートはそのホルダーに差し替わって無効化されている。ここはそれらにだけスプライトを適用する。
        // ※ "Visual" 子はスプライト参照が未解決(不可視)の飾り枠。ボス画像を入れると静止した二重表示になるため触らない。
        // ※ sr.enabled も触らない(FloatingCharacterMotion の「ルート無効・ホルダー表示」状態を保つ)。
        foreach (var sr in enemyGo.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            if (sr.gameObject.name == "Visual") continue;   // 飾り枠(不可視)はボス画像に差し替えない
            sr.color = Color.white;
            if (sprite != null) sr.sprite = sprite;
        }
        enemyGo.SetActive(true);   // 表示/非表示は GameObject 単位で行う(個別 enabled 状態を保つ)
    }

    /// <summary>破壊演出用の破片(小さな円)を放射状に飛ばす。全破片を1つのコンテナ下にまとめて返す(呼び出し側で破棄)。</summary>
    private GameObject SpawnExplosion(Vector3 center, float radius)
    {
        var container = new GameObject("BossExplosion");
        container.transform.position = center;
        var sprite = GetParticleSprite();
        int n = 28;
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("BossShard");
            go.transform.SetParent(container.transform, false);
            float size = Random.Range(0.15f, 0.42f) * radius;
            go.transform.localScale = Vector3.one * size;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1000;
            sr.color = Color.Lerp(new Color(1f, 0.85f, 0.3f, 1f), new Color(1f, 0.4f, 0.2f, 1f), Random.value);

            float ang = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            float spd = Random.Range(3.5f, 9.5f) * Mathf.Max(0.6f, radius);
            Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;

            StartCoroutine(AnimateShard(go.transform, sr, vel));
        }
        return container;
    }

    private IEnumerator AnimateShard(Transform tr, SpriteRenderer sr, Vector2 vel)
    {
        float life = Random.Range(0.5f, 0.95f), t = 0f;
        while (t < life && tr != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            vel *= 0.92f;          // 空気抵抗で減速
            vel.y -= 7f * dt;      // 重力で落下
            tr.position += (Vector3)(vel * dt);
            tr.localScale *= (1f - 1.2f * dt);
            if (sr != null) { Color c = sr.color; c.a = 1f - (t / life); sr.color = c; }
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }

    private static Sprite _particleSprite;
    private static Sprite GetParticleSprite()
    {
        if (_particleSprite != null) return _particleSprite;
        int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c - 1f;
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(rad - d + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        _particleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        _particleSprite.name = "BossShardParticle";
        return _particleSprite;
    }

    // GUI用アクセサ
    public string GetCurrentMessage() => currentMessage;
    public float GetMessageAlpha() => messageAlpha;
    public List<ResultEntry> GetResultEntries() => resultEntries;
    public bool IsBossDefeated() => bossDefeated;
    public List<string> GetDestroyedParts() => destroyedPartsThisTurn;
}

/// <summary>BattleResultPhase用のIMGUIヘルパー</summary>
public class BattleResultIMGUIHelper : MonoBehaviour
{
    private BattleResultPhase phase;
    private int pixelScale;
    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

    public void Initialize(BattleResultPhase p)
    {
        phase = p;
        pixelScale = Mathf.Max(3, Screen.height / 240);
    }

    void OnDestroy()
    {
        foreach (var tex in textureCache.Values)
            if (tex != null) Destroy(tex);
        textureCache.Clear();
    }

    void OnGUI()
    {
        if (phase == null) return;

        // メインメッセージ
        string msg = phase.GetCurrentMessage();
        if (!string.IsNullOrEmpty(msg))
        {
            DrawCenteredText(msg, Screen.height * 0.15f, pixelScale + 2, Color.white);
        }

        // 結果エントリー表示
        var entries = phase.GetResultEntries();
        float startY = Screen.height * 0.3f;
        float entryHeight = 50f;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            float y = startY + i * entryHeight;
            DrawResultEntry(entry, y);
        }

        // ボス撃破時の特別表示
        if (phase.IsBossDefeated())
        {
            DrawCenteredText("VICTORY!", Screen.height * 0.5f, pixelScale + 4, new Color(1f, 0.85f, 0.2f));
        }
    }

    private void DrawResultEntry(BattleResultPhase.ResultEntry entry, float y)
    {
        float centerX = Screen.width / 2f;

        // プレイヤー番号
        string playerLabel = $"P{entry.playerIndex + 1}";
        DrawText(playerLabel, centerX - 200, y, pixelScale, entry.playerColor);

        // カード名
        DrawText(entry.cardName, centerX - 100, y, pixelScale, Color.white);

        // 結果
        string resultText;
        Color resultColor;
        if (entry.success)
        {
            if (entry.category == CardCategory.Attack)
            {
                resultText = $"-{entry.damage}";
                resultColor = new Color(1f, 0.4f, 0.4f);
            }
            else if (entry.category == CardCategory.Heal)
            {
                resultText = $"+{entry.healAmount}";
                resultColor = new Color(0.4f, 1f, 0.5f);
            }
            else
            {
                resultText = "OK";
                resultColor = Color.cyan;
            }
        }
        else
        {
            resultText = "MISS";
            resultColor = Color.gray;
        }
        DrawText(resultText, centerX + 100, y, pixelScale + 1, resultColor);
    }

    private void DrawCenteredText(string text, float y, int scale, Color color)
    {
        var tex = GetOrCreateTexture(text, color, scale);
        if (tex != null)
        {
            float x = (Screen.width - tex.width) / 2f;
            GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
        }
    }

    private void DrawText(string text, float x, float y, int scale, Color color)
    {
        var tex = GetOrCreateTexture(text, color, scale);
        if (tex != null)
        {
            GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
        }
    }

    private Texture2D GetOrCreateTexture(string text, Color color, int scale)
    {
        string key = $"{text}_{color.r:F2}_{color.g:F2}_{color.b:F2}_{scale}";
        if (!textureCache.TryGetValue(key, out var tex) || tex == null)
        {
            tex = PixelFontRenderer.RenderText(text, color, scale);
            textureCache[key] = tex;
        }
        return tex;
    }
}
