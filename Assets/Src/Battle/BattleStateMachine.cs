using System.Collections.Generic;
using UnityEngine;
// 新しい Input System を利用するための名前空間。パッケージが有効な場合に使用します。
using UnityEngine.InputSystem;

/// <summary>
/// 戦闘の簡易ステートマシン。
/// 各フェーズは IBattlePhase を実装し、Enter/Exit/Tick が呼ばれます。
/// </summary>
public class BattleStateMachine : MonoBehaviour
{
    /// <summary>
    /// 利用するステートの列挙。
    /// 必要に応じてここに状態を追加してください。
    /// </summary>
    public enum State
    {
        BossIntro,
        CardSelect,
        PartSelect,
        ActionCommand,
        BattleResult,
        Dodge,
    }

    [Header("フェーズコンポーネント(Inspectorで割り当て、未割当の場合は Awake で探索されます)")]
    [SerializeField] private BossIntroPhase bossIntroPhase;
    [SerializeField] private CardSelectPhase cardSelectPhase;
    [SerializeField] private PartSelectPhase partSelectPhase;
    [SerializeField] private ActionCommandPhase actionCommandPhase;
    [SerializeField] private BattleResultPhase battleResultPhase;
    [SerializeField] private DodgePhase dodgePhase;

    [Header("起動")]
    [Tooltip("Start で最初に入る状態。")]
    public State initialState = State.BossIntro;
    [Tooltip("GameFont の初期化に使う GameTuning。displayFont / bodyFont があれば全 UI に反映。")]
    [SerializeField] private GameTuning fontTuning;

    // 現在アクティブなフェーズ実装への参照
    private IBattlePhase currentPhase;
    // 現在の列挙値としての状態
    private State currentState;

    /// <summary>現在のフェーズ状態(デバッグメニュー等から参照)。</summary>
    public State CurrentState => currentState;

    private void Awake()
    {

        // フォント: GameTuning.displayFont / bodyFont を全 UI に反映(任意)
        if (fontTuning == null)
        {
            // 自動探索: シーン内の GameTuning ScriptableObject を Inspector から拾えなければ、
            // CardSelectPhase 等が持ってる tuning を試す
            var anyTuning = Resources.FindObjectsOfTypeAll<GameTuning>();
            if (anyTuning != null && anyTuning.Length > 0) fontTuning = anyTuning[0];
        }
        if (fontTuning != null) GameFont.RegisterFromTuning(fontTuning);

        // Inspector にセットされていなければ子オブジェクトから自動取得する
        if (bossIntroPhase == null) bossIntroPhase = GetComponentInChildren<BossIntroPhase>();
        if (cardSelectPhase == null) cardSelectPhase = GetComponentInChildren<CardSelectPhase>();
        if (partSelectPhase == null) partSelectPhase = GetComponentInChildren<PartSelectPhase>();
        if (actionCommandPhase == null) actionCommandPhase = GetComponentInChildren<ActionCommandPhase>();
        if (battleResultPhase == null) battleResultPhase = GetComponentInChildren<BattleResultPhase>();
        if (dodgePhase == null) dodgePhase = GetComponentInChildren<DodgePhase>();

        // BossIntroPhase が見つからなければ自動生成
        if (bossIntroPhase == null)
        {
            Debug.Log("[BattleStateMachine] BossIntroPhaseを自動生成します");
            var introGo = new GameObject("BossIntroPhase");
            introGo.transform.SetParent(transform, false);
            bossIntroPhase = introGo.AddComponent<BossIntroPhase>();
            Debug.Log($"[BattleStateMachine] BossIntroPhase生成完了: {bossIntroPhase != null}");
        }

        // BattleResultPhase が見つからなければ自動生成
        if (battleResultPhase == null)
        {
            var resultGo = new GameObject("BattleResultPhase");
            resultGo.transform.SetParent(transform, false);
            battleResultPhase = resultGo.AddComponent<BattleResultPhase>();
        }

        // PartyDeckControllerにデフォルトデッキを設定
        EnsurePartyDeckController();

        // バトル背景をシーンに無ければ自動生成
        EnsureBattleBackground();
        // 弾幕フレームをシーンに無ければ自動生成
        EnsureDanmakuFrameView();
        // HPバーをシーンに無ければ自動生成
        EnsureSharedHpGaugeUI();
        // バトル効果プロセッサを自動生成
        EnsureBattleEffectProcessor();
        // 部位破壊演出UIを自動生成
        EnsurePartDestroyEffectUI();
        // URP 2D用のGlobal Light 2Dを自動生成
        EnsureGlobalLight2D();
        // SpriteRendererのマテリアルをUnlitに修正
        EnsureSpriteRendererMaterialFixer();
        // カメラデバッガーを追加
        EnsureCameraDebugger();
    }

    /// <summary>
    /// PartyDeckController にデッキが設定されていない場合、デフォルトデッキを設定する。
    /// </summary>
    private void EnsurePartyDeckController()
    {
        var deckController = GetComponent<PartyDeckController>();
        if (deckController == null)
        {
            deckController = FindObjectOfType<PartyDeckController>();
        }
        if (deckController == null)
        {
            Debug.LogError("[BattleStateMachine] PartyDeckController が見つかりません！BattleManagerオブジェクトにアタッチしてください。");
            return;
        }

        // perPlayerDecks が空の場合、Resources から StandardDeck をロードして設定
        if (deckController.perPlayerDecks == null || deckController.perPlayerDecks.Count == 0)
        {
            var standardDeck = Resources.Load<DeckDefinition>("Data/StandardDeck");
            if (standardDeck != null)
            {
                deckController.perPlayerDecks = new List<DeckDefinition>();
                for (int i = 0; i < deckController.playerCount; i++)
                {
                    deckController.perPlayerDecks.Add(standardDeck);
                }
                Debug.Log($"[BattleStateMachine] StandardDeck を {deckController.playerCount} 人分設定しました");
            }
            else
            {
                Debug.LogError("[BattleStateMachine] Resources/Data/StandardDeck.asset が見つかりません！");
            }
        }
    }

    /// <summary>
    /// BattleEffectProcessor がシーンに存在しない場合、自動生成する。
    /// </summary>
    private void EnsureBattleEffectProcessor()
    {
        if (FindObjectOfType<BattleEffectProcessor>() != null) return;

        var processorGo = new GameObject("BattleEffectProcessor");
        processorGo.transform.SetParent(transform, false);
        processorGo.AddComponent<BattleEffectProcessor>();
    }

    /// <summary>
    /// PartDestroyEffectUI がシーンに存在しない場合、自動生成する。
    /// </summary>
    private void EnsurePartDestroyEffectUI()
    {
        if (FindObjectOfType<PartDestroyEffectUI>() != null) return;

        var effectGo = new GameObject("PartDestroyEffectUI");
        effectGo.transform.SetParent(transform, false);
        effectGo.AddComponent<PartDestroyEffectUI>();
    }

    /// <summary>
    /// URP 2D Renderer用のGlobal Light 2Dがシーンに存在しない場合、自動生成する。
    /// Sprite Lit 2D Materialを使用するスプライトはLight2Dがないと暗くなる。
    /// </summary>
    private void EnsureGlobalLight2D()
    {
        if (FindObjectOfType<GlobalLight2DSetup>() != null) return;

        var lightSetupGo = new GameObject("GlobalLight2DSetup");
        lightSetupGo.transform.SetParent(transform, false);
        lightSetupGo.AddComponent<GlobalLight2DSetup>();
        Debug.Log("[BattleStateMachine] GlobalLight2DSetupを自動生成しました");
    }

    /// <summary>
    /// SpriteRendererのマテリアルをSprites-Default（Unlit）に修正する。
    /// URP 2D環境でLight2Dなしでもスプライトが表示されるようにする。
    /// </summary>
    private void EnsureSpriteRendererMaterialFixer()
    {
        if (FindObjectOfType<SpriteRendererMaterialFixer>() != null) return;

        var fixerGo = new GameObject("SpriteRendererMaterialFixer");
        fixerGo.transform.SetParent(transform, false);
        fixerGo.AddComponent<SpriteRendererMaterialFixer>();
        Debug.Log("[BattleStateMachine] SpriteRendererMaterialFixerを自動生成しました");
    }

    /// <summary>
    /// CameraDebugger がシーンに存在しない場合、自動生成する。
    /// </summary>
    private void EnsureCameraDebugger()
    {
        if (FindObjectOfType<CameraDebugger>() != null) return;

        var debugGo = new GameObject("CameraDebugger");
        debugGo.transform.SetParent(transform, false);
        debugGo.AddComponent<CameraDebugger>();
    }

    /// <summary>
    /// BattleBackgroundView がシーンに存在しない場合、自動生成する。
    /// </summary>
    private void EnsureBattleBackground()
    {
        if (FindObjectOfType<BattleBackgroundView>() != null) return;

        var bgGo = new GameObject("BattleBackground");
        bgGo.AddComponent<BattleBackgroundView>();
        // OnGUI の描画順を最背面にするため、最初の子として配置
        bgGo.transform.SetAsFirstSibling();
    }

    /// <summary>
    /// DanmakuFrameView がシーンに存在しない場合、自動生成する。
    /// </summary>
    private void EnsureDanmakuFrameView()
    {
        if (FindObjectOfType<DanmakuFrameView>() != null) return;

        var frameGo = new GameObject("DanmakuFrame");
        frameGo.AddComponent<DanmakuFrameView>();
    }

    /// <summary>
    /// SharedHpGaugeUI がシーンに存在しない場合、自動生成する。
    /// </summary>
    private void EnsureSharedHpGaugeUI()
    {
        if (FindObjectOfType<SharedHpGaugeUI>() != null) return;

        var hpGaugeGo = new GameObject("SharedHpGaugeUI");
        var hpGaugeUI = hpGaugeGo.AddComponent<SharedHpGaugeUI>();

        // SharedHpControllerを探してアサイン
        var hpController = GetComponent<SharedHpController>();
        if (hpController == null)
        {
            hpController = FindObjectOfType<SharedHpController>();
        }

        // リフレクションでsourceフィールドを設定
        if (hpController != null)
        {
            var sourceField = typeof(SharedHpGaugeUI).GetField("source",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (sourceField != null)
            {
                sourceField.SetValue(hpGaugeUI, hpController);
            }
        }
    }

    private void Start()
    {
        Debug.Log("[BattleStateMachine] Start 開始");
        Debug.Log($"[BattleStateMachine] initialState (before override): {initialState} (int: {(int)initialState})");

        // 強制的にBossIntroから開始（シーンのシリアライズ値が古い場合の対策）
        initialState = State.BossIntro;
        Debug.Log($"[BattleStateMachine] initialState (after override): {initialState}");
        Debug.Log($"[BattleStateMachine] bossIntroPhase: {(bossIntroPhase != null ? "OK" : "NULL")}");
        Debug.Log($"[BattleStateMachine] cardSelectPhase: {(cardSelectPhase != null ? "OK" : "NULL")}");
        Debug.Log($"[BattleStateMachine] partSelectPhase: {(partSelectPhase != null ? "OK" : "NULL")}");
        Debug.Log($"[BattleStateMachine] actionCommandPhase: {(actionCommandPhase != null ? "OK" : "NULL")}");
        Debug.Log($"[BattleStateMachine] dodgePhase: {(dodgePhase != null ? "OK" : "NULL")}");

        // キャラクタースプライトの検証
        ValidateCharacterSprites();

        // すべてのフェーズをいったん Exit 状態にしてから開始する
        Debug.Log("[BattleStateMachine] フェーズExit開始");
        bossIntroPhase?.Exit();
        Debug.Log("[BattleStateMachine] bossIntroPhase.Exit完了");
        cardSelectPhase?.Exit();
        Debug.Log("[BattleStateMachine] cardSelectPhase.Exit完了");
        partSelectPhase?.Exit();
        Debug.Log("[BattleStateMachine] partSelectPhase.Exit完了");
        actionCommandPhase?.Exit();
        Debug.Log("[BattleStateMachine] actionCommandPhase.Exit完了");
        battleResultPhase?.Exit();
        Debug.Log("[BattleStateMachine] battleResultPhase.Exit完了");
        dodgePhase?.Exit();
        Debug.Log("[BattleStateMachine] dodgePhase.Exit完了");

        // HPバーを非表示（BossIntroフェーズ中は表示しない、DodgePhaseで表示される）
        var hpGaugeUI = FindObjectOfType<SharedHpGaugeUI>();
        if (hpGaugeUI != null)
        {
            hpGaugeUI.SetVisible(false);
        }

        // 初期状態。
        Debug.Log($"[BattleStateMachine] 初期状態に遷移: {initialState}");
        ChangeState(initialState);
    }

    /// <summary>
    /// シーン内のキャラクタースプライトを検証し、問題があれば自動修復を試みる。
    /// </summary>
    private void ValidateCharacterSprites()
    {
        // プレイヤースプライトをResourcesから事前ロード
        var playerSprites = Resources.LoadAll<Sprite>("Images/Player");
        if (playerSprites == null || playerSprites.Length == 0)
        {
            Debug.LogWarning("[BattleStateMachine] Resources/Images/Player からスプライトを読み込めませんでした。スプライト自動修復は無効です。");
        }

        // CardSelectPhaseのplayerTransformsを検証
        if (cardSelectPhase != null)
        {
            var playerTransformsField = typeof(CardSelectPhase).GetField("playerTransforms",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (playerTransformsField != null)
            {
                var playerTransforms = playerTransformsField.GetValue(cardSelectPhase) as Transform[];
                if (playerTransforms == null || playerTransforms.Length == 0)
                {
                    Debug.LogError("[BattleStateMachine] CardSelectPhase.playerTransforms が設定されていません！シーンにプレイヤーキャラクターを配置し、CardSelectPhaseのInspectorで設定してください。");
                }
                else
                {
                    for (int i = 0; i < playerTransforms.Length; i++)
                    {
                        if (playerTransforms[i] == null)
                        {
                            Debug.LogError($"[BattleStateMachine] playerTransforms[{i}] がnullです！");
                            continue;
                        }
                        var sr = playerTransforms[i].GetComponent<SpriteRenderer>();
                        if (sr == null)
                        {
                            Debug.LogError($"[BattleStateMachine] playerTransforms[{i}] ({playerTransforms[i].name}) にSpriteRendererがありません！");
                        }
                        else if (sr.sprite == null)
                        {
                            Debug.LogWarning($"[BattleStateMachine] playerTransforms[{i}] ({playerTransforms[i].name}) のスプライトがnullです。自動修復を試みます...");
                            TryRepairPlayerSprite(sr, i, playerSprites);
                        }
                        else
                        {
                            Debug.Log($"[BattleStateMachine] playerTransforms[{i}]: {playerTransforms[i].name}, sprite={sr.sprite.name} - OK");
                        }
                    }
                }
            }
        }

        // Playerタグが付いたオブジェクトも検証・修復
        var playerObjects = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < playerObjects.Length; i++)
        {
            var sr = playerObjects[i].GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite == null)
            {
                Debug.LogWarning($"[BattleStateMachine] {playerObjects[i].name} のスプライトがnullです。自動修復を試みます...");
                TryRepairPlayerSprite(sr, i, playerSprites);
            }
        }

        // カメラ情報をログ出力
        var cam = Camera.main;
        if (cam != null)
        {
            Debug.Log($"[BattleStateMachine] === カメラ情報 ===");
            Debug.Log($"  位置: {cam.transform.position}");
            Debug.Log($"  orthographicSize: {cam.orthographicSize}");
            Debug.Log($"  aspect: {cam.aspect}");
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Debug.Log($"  表示範囲: X[{-halfW:F1} ~ {halfW:F1}], Y[{-halfH:F1} ~ {halfH:F1}]");
        }

        // シーン内のすべてのSpriteRendererを検証・デバッグ出力
        var allSpriteRenderers = FindObjectsOfType<SpriteRenderer>();
        int missingCount = 0;
        Debug.Log($"[BattleStateMachine] === SpriteRenderer一覧 (計{allSpriteRenderers.Length}個) ===");
        foreach (var sr in allSpriteRenderers)
        {
            string spriteName = sr.sprite != null ? sr.sprite.name : "NULL";
            Debug.Log($"  {sr.gameObject.name}: pos={sr.transform.position}, sprite={spriteName}, sortingOrder={sr.sortingOrder}, enabled={sr.enabled}, color.a={sr.color.a}");

            if (sr.sprite == null && sr.gameObject.activeInHierarchy)
            {
                // 背景やUIなど意図的にspriteがnullのものは除外（動的にスプライト設定されるオブジェクト含む）
                string objName = sr.gameObject.name;
                if (objName.Contains("Background") ||
                    objName.Contains("Backdrop") ||
                    objName.Contains("_Temp") ||
                    objName.Contains("Frame") ||
                    objName.Contains("Gauge") ||
                    objName.Contains("HpFill") ||
                    objName.Contains("Border") ||
                    objName.Contains("Boss") ||
                    objName.Contains("Enemy") ||
                    objName.Contains("Player") ||
                    objName == "Enemy") continue;
                missingCount++;
                Debug.LogWarning($"[BattleStateMachine] スプライト未設定: {sr.gameObject.name}");
            }
        }
        if (missingCount > 0)
        {
            Debug.LogError($"[BattleStateMachine] {missingCount}個のSpriteRendererにスプライトが設定されていません！");
        }
    }

    /// <summary>
    /// プレイヤーのスプライトを自動修復する
    /// </summary>
    private void TryRepairPlayerSprite(SpriteRenderer sr, int playerIndex, Sprite[] playerSprites)
    {
        if (playerSprites == null || playerSprites.Length == 0)
        {
            Debug.LogError($"[BattleStateMachine] スプライト修復失敗: playerSpritesが空です");
            return;
        }

        // Player_X という名前のスプライトを探す
        string targetName = $"Player_{playerIndex}";
        Sprite targetSprite = null;

        foreach (var sprite in playerSprites)
        {
            if (sprite.name == targetName)
            {
                targetSprite = sprite;
                break;
            }
        }

        if (targetSprite != null)
        {
            sr.sprite = targetSprite;
            Debug.Log($"[BattleStateMachine] {sr.gameObject.name} にスプライト {targetSprite.name} を設定しました");
        }
        else if (playerIndex < playerSprites.Length)
        {
            // インデックスでフォールバック
            sr.sprite = playerSprites[playerIndex];
            Debug.Log($"[BattleStateMachine] {sr.gameObject.name} にスプライト {playerSprites[playerIndex].name} を設定しました（インデックスフォールバック）");
        }
        else
        {
            Debug.LogError($"[BattleStateMachine] スプライト修復失敗: Player_{playerIndex} が見つかりません");
        }
    }

    private void Update()
    {
        // フェーズ Tick 中に ChangeState されたかを検知するため遷移前の状態を保持
        State prevState = currentState;

        // 毎フレーム現在フェーズの Tick を呼ぶ
        currentPhase?.Tick();

        // Tick 内でフェーズ遷移が起きていれば、デバッグ Enter の二重発火を防ぐためここで打ち切る
        if (currentState != prevState) return;

        // フェーズが Enter を自前で消費する場合(例: カード選択中)はデバッグ自動遷移しない
        if (currentPhase != null && currentPhase.BlocksDebugAdvance) return;

        // デバッグ用: Enter キーで次のフェーズへ切り替える（テンポラリ）
        // 実装後は入力管理に移すことを推奨
        bool enterPressed = false;

        // 1) 新しい Input System パッケージ経由でのチェック（プロジェクトが Input System を使っている場合）
        // Keyboard.current が null でないことを確認してから参照する
        if (Keyboard.current != null)
        {
            enterPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        }

        // 2) 旧来の Input.System (UnityEngine.Input) をフォールバックで試す。
        // Player Settings が新しい Input System に切り替わっていると例外が投げられるため try/catch で保護する。
        if (!enterPressed)
        {
            try
            {
                enterPressed = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter);
            }
            catch (System.InvalidOperationException)
            {
                // Legacy Input が無効な場合は何もしない（enterPressed は false のまま）
            }
        }

        if (enterPressed)
        {
            NextState();
        }
    }

    /// <summary>
    /// ステート遷移を行う。
    /// 同じステートへの遷移は無視されます。
    /// </summary>
    public void ChangeState(State newState)
    {
        if (currentPhase != null && newState == currentState) return;

        // 現在のフェーズを抜ける
        currentPhase?.Exit();

        currentState = newState;

        // 新しい状態に対応するフェーズへ差し替え
        switch (newState)
        {
            case State.BossIntro: currentPhase = bossIntroPhase; break;
            case State.CardSelect: currentPhase = cardSelectPhase; break;
            case State.PartSelect: currentPhase = partSelectPhase; break;
            case State.ActionCommand: currentPhase = actionCommandPhase; break;
            case State.BattleResult: currentPhase = battleResultPhase; break;
            case State.Dodge: currentPhase = dodgePhase; break;
        }

        // 新しいフェーズへ入る
        currentPhase?.Enter();

        // デバッグ: 現在のステートをログに出す
        UnityEngine.Debug.Log($"[BattleStateMachine] 状態遷移 -> {currentState}");
    }

    /// <summary>
    /// 現在の状態から次の状態へ循環的に遷移するユーティリティ。
    /// デバッグやキー入力での切替に使いやすい。
    /// </summary>
    public void NextState()
    {
        // 列挙型を int に変換してインクリメント、範囲をラップする
        int next = ((int)currentState + 1) % System.Enum.GetValues(typeof(State)).Length;
        ChangeState((State)next);
    }
}
