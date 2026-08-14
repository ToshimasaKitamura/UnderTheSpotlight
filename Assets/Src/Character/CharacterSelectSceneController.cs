using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// キャラクター選択シーンのコントローラー（スポットライト演出版）
/// 複数プレイヤーが順番にキャラクターを選択する
/// </summary>
public class CharacterSelectSceneController : MonoBehaviour
{
    [Header("キャラクターデータ")]
    [Tooltip("選択可能なキャラクターリスト")]
    public List<CharacterData> availableCharacters = new List<CharacterData>();

    [Header("UI")]
    [Tooltip("スポットライトビュー")]
    public CharacterSpotlightView spotlightView;
    [Tooltip("プレイヤー選択リストビュー")]
    public PlayerSelectionListView playerListView;
    [Tooltip("キャラクター選択グリッドビュー")]
    public CharacterSelectView gridView;

    [Header("ゲーム設定")]
    [Tooltip("プレイヤー人数（1〜4人）")]
    public int numberOfPlayers = 4;

    [Header("サウンド")]
    [Tooltip("カーソル移動音")]
    public AudioClip cursorMoveSfx;
    [Tooltip("決定音")]
    public AudioClip confirmSfx;

    private AudioSource audioSource;
    private int[] playerCursorIndices = new int[4];
    private bool[] isPlayerConfirmed = new bool[4];
    // ステージセレクト(ロビー)で募集した参加プレイヤー。非参加はカーソルを出さず・決定も要求しない。
    private bool[] participating = new bool[4] { true, true, true, true };
    private CharacterData[] selectedCharacters;
    private List<int> availableIndices = new List<int>();
    
    // デバッグ用: 現在操作中のプレイヤー（0=自動/全, 1-4=固定）
    private int debugActivePlayer = 0;
    private int lastInputPlayerIndex = 0;

    // カーソル(各プレイヤー色のマウスポインタ絵)。カード選択と同じくキーボードで動かす。
    // 数字キー(1〜4)を押しながら WASD/矢印でそのプレイヤーのカーソルを移動。無ければ activePlayer。
    private Canvas cursorCanvas;
    private RectTransform[] cursorRT;
    private UnityEngine.UI.Image[] cursorImg;
    private Vector2[] cursorPos;
    private int activePlayer = 0;
    private int lastActiveVisual = -1; // カーソル明暗の再描画差分用
    private const float CursorSpeed = 900f; // カーソル移動速度(px/秒)

    private float inputCooldown = 0f;
private const float INPUT_COOLDOWN_TIME = 0.15f;
    private bool stickPressed = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // ビューがない場合はエラー
        if (spotlightView == null)
        {
            Debug.LogError("[CharacterSelectSceneController] CharacterSpotlightView が設定されていません。");
        }

        if (playerListView == null)
        {
            Debug.LogError("[CharacterSelectSceneController] PlayerSelectionListView が設定されていません。");
        }

        // 4名分固定
        numberOfPlayers = 4;
        selectedCharacters = new CharacterData[4];
        playerCursorIndices = new int[4];
        isPlayerConfirmed = new bool[4];
    }

    void Start()
    {
        Debug.Log($"[CharacterSelect] Start() - availableCharacters.Count = {availableCharacters.Count}");

        // Unity 6 では旧既定フォント(Arial)が無く uGUI Text が空白になる。全 Text に規定フォントを設定。
        ApplyFontToAllTexts();

        // null 除去
        availableCharacters.RemoveAll(c => c == null);

        // キャラクターデータがない場合はデフォルト作成
        if (availableCharacters.Count == 0)
        {
            CreateDefaultCharacters();
        }

        // 利用可能なキャラクターインデックスリストを初期化
        availableIndices.Clear();
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            availableIndices.Add(i);
        }

        // プレイヤー初期位置
        for (int i = 0; i < 4; i++)
        {
            playerCursorIndices[i] = i % availableCharacters.Count;
            isPlayerConfirmed[i] = false;
        }

        if (gridView != null)
        {
            gridView.Build(availableCharacters);
            gridView.SetLocked(false);
            gridView.SetPlayerCursors(new int[] { -1, -1, -1, -1 }); // 旧ボタン方式の色枠を全消灯
        }

        if (playerListView != null)
        {
            playerListView.ClearAll();
        }

        ResolveParticipants(); // ステージセレクトで募集した参加者を反映
        CreateCursors();   // 各プレイヤーのカーソルを生成(キーボード選択は撤廃)
        UpdateUI();
    }

    /// <summary>
    /// ステージセレクト(ロビー)で募集した参加プレイヤーを取り込む。
    /// 募集情報が無い(直接シーン再生)場合のみ、開発用に4人全員参加として扱う。
    /// </summary>
    private void ResolveParticipants()
    {
        var gsm = GameSceneManager.Instance;
        if (gsm != null && gsm.JoinedCount >= 1)
        {
            for (int i = 0; i < 4; i++) participating[i] = gsm.joinedPlayers[i];
        }
        else
        {
            for (int i = 0; i < 4; i++) participating[i] = true;
            Debug.LogWarning("[CharacterSelect] ロビーの募集情報がありません(直接再生とみなす)。4人全員参加として進めます。");
        }

        // アクティブ/表示の初期プレイヤーを最初の参加者にする
        for (int i = 0; i < 4; i++)
        {
            if (participating[i]) { activePlayer = i; lastInputPlayerIndex = i; break; }
        }
    }

    void Update()
    {
        if (inputCooldown > 0)
        {
            inputCooldown -= Time.deltaTime;
        }

        HandleInput();
    }

    // カーソルをキーボードで動かす方式(カード選択と同じ)。
    // WASD/矢印で移動。数字キー(1〜4)押しっぱなしでそのプレイヤーのカーソルを操作、無ければ activePlayer。
    // ホバー中のカードに重ねて Enter/Space/Z で決定。
    private void HandleInput()
    {
        if (cursorRT == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        // 移動方向
        Vector2 dir = Vector2.zero;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir.x -= 1f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir.x += 1f;
        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dir.y += 1f;
        if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dir.y -= 1f;
        if (dir.sqrMagnitude > 1f) dir = dir.normalized;

        // 操作対象: 数字キー押しっぱなし(複数可)。無ければ activePlayer のみ。
        bool[] holdP = { kb.digit1Key.isPressed, kb.digit2Key.isPressed, kb.digit3Key.isPressed, kb.digit4Key.isPressed };
        bool anyHeld = holdP[0] || holdP[1] || holdP[2] || holdP[3];

        // 数字キーを押している間は、そのプレイヤーが「選んでいるプレイヤー(=アクティブ)」になる。
        if (anyHeld)
        {
            for (int p = 0; p < numberOfPlayers; p++)
                if (holdP[p] && participating[p]) { activePlayer = p; break; }
        }

        // カーソル移動: 数字キー押下中はそのプレイヤー(複数可)、無ければ activePlayer のみ。
        // ※非アクティブのカーソルも数字キー併用で動かせる（決定は下で弾く）。参加者のみ。
        if (dir != Vector2.zero)
        {
            for (int p = 0; p < numberOfPlayers; p++)
            {
                bool move = anyHeld ? holdP[p] : (p == activePlayer);
                if (!move || !participating[p]) continue;
                cursorPos[p] += dir * CursorSpeed * Time.deltaTime;
                cursorPos[p].x = Mathf.Clamp(cursorPos[p].x, 0f, Screen.width);
                cursorPos[p].y = Mathf.Clamp(cursorPos[p].y, 0f, Screen.height);
                if (cursorRT[p] != null) cursorRT[p].position = cursorPos[p];
            }
        }

        RefreshCursorVisual();  // アクティブ以外を減光

        // ホバー中のカードを各プレイヤーの選択に反映(変化時のみ更新)
        bool changed = false;
        for (int p = 0; p < numberOfPlayers; p++)
        {
            if (!participating[p] || isPlayerConfirmed[p]) continue;
            int hover = CardUnderCursor(cursorPos[p]);
            if (hover >= 0 && hover != playerCursorIndices[p])
            {
                playerCursorIndices[p] = hover;
                lastInputPlayerIndex = p;
                changed = true;
            }
        }
        if (changed) { PlaySound(cursorMoveSfx); UpdateUI(); }

        // 決定: 「選んでいるプレイヤー(activePlayer)」のみ受け付ける。
        // 非アクティブのカーソルは決定入力を受け付けない（暗いまま移動のみ可）。
        if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
        {
            int p = activePlayer;
            if (p >= 0 && p < numberOfPlayers && participating[p] && !isPlayerConfirmed[p])
            {
                int hover = CardUnderCursor(cursorPos[p]);
                if (hover >= 0)
                {
                    playerCursorIndices[p] = hover;
                    lastInputPlayerIndex = p;
                    ConfirmPlayerSelection(p);
                    PlaySound(confirmSfx);
                    UpdateUI();
                }
            }
        }

        // 戻る
        if (kb.escapeKey.wasPressedThisFrame)
            GameSceneManager.Instance?.LoadWorldMapScene();
    }

    private Camera _hitCam;
    private bool _hitCamResolved;
    /// <summary>カードが載る Canvas の当たり判定用カメラ(Overlay なら null、Camera/World ならその camera)。</summary>
    private Camera HitCamera()
    {
        if (_hitCamResolved) return _hitCam;
        _hitCamResolved = true;
        var canvas = gridView != null ? gridView.GetComponentInParent<Canvas>() : null;
        if (canvas != null)
        {
            canvas = canvas.rootCanvas;
            _hitCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        }
        else _hitCam = Camera.main;
        return _hitCam;
    }

    /// <summary>スクリーン座標の下にあるカードのインデックス(なければ -1)。</summary>
    private int CardUnderCursor(Vector2 screenPos)
    {
        if (gridView == null || gridView.characterCards == null) return -1;
        var cam = HitCamera();
        var cards = gridView.characterCards;
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null || !c.gameObject.activeSelf) continue;
            var rt = c.transform as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam))
                return i;
        }
        return -1;
    }

    /// <summary>各プレイヤー色のカーソル(リング)を生成。OSカーソルは隠す。</summary>
    private void CreateCursors()
    {
        var go = new GameObject("CharSelectCursors", typeof(Canvas));
        cursorCanvas = go.GetComponent<Canvas>();
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.sortingOrder = 500;

        cursorRT = new RectTransform[numberOfPlayers];
        cursorImg = new UnityEngine.UI.Image[numberOfPlayers];
        cursorPos = new Vector2[numberOfPlayers];
        for (int p = 0; p < numberOfPlayers; p++)
        {
            // 非参加プレイヤーはカーソルを出さない(cursorRT[p]/cursorImg[p] は null のまま)
            if (!participating[p]) continue;

            var img = new GameObject($"Cursor{p + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image))
                .GetComponent<UnityEngine.UI.Image>();
            img.transform.SetParent(cursorCanvas.transform, false);
            img.sprite = CursorSprite.CreatePointer(PlayerColor(p));  // ゲーム共通のマウスカーソル絵
            img.raycastTarget = false;
            cursorRT[p] = img.rectTransform;
            cursorImg[p] = img;
            cursorRT[p].pivot = CursorSprite.TipPivot;                // 先端がカーソル座標に来る
            cursorRT[p].sizeDelta = new Vector2(58f, 58f);
            cursorPos[p] = new Vector2(Screen.width * (0.32f + 0.12f * p), Screen.height * 0.55f);
            cursorRT[p].position = cursorPos[p];
        }
        RefreshCursorVisual();
    }

    /// <summary>
    /// 「選んでいるプレイヤー(activePlayer)」のカーソルだけをアクティブ表示にし、
    /// それ以外は暗く(半透明)する。非アクティブでも移動自体は可能（決定は受け付けない）。
    /// </summary>
    private void RefreshCursorVisual()
    {
        if (cursorImg == null) return;
        for (int p = 0; p < numberOfPlayers; p++)
        {
            if (cursorImg[p] == null) continue;
            // sprite に色が焼き込まれているため、白=そのまま / グレー低アルファ=減光。
            cursorImg[p].color = (p == activePlayer)
                ? Color.white
                : new Color(0.5f, 0.5f, 0.5f, 0.4f);
        }
        // アクティブなカーソルを最前面へ(切替時のみ)
        if (activePlayer != lastActiveVisual)
        {
            if (activePlayer >= 0 && activePlayer < cursorRT.Length && cursorRT[activePlayer] != null)
                cursorRT[activePlayer].SetAsLastSibling();
            lastActiveVisual = activePlayer;
        }
    }

    private static Color PlayerColor(int p)
    {
        var pal = GameColorScheme.Players;
        return (pal != null && p >= 0 && p < pal.Length) ? pal[p] : Color.white;
    }


    private void ApplyFontToAllTexts()
    {
        var font = GameFont.Body();
        if (font == null) return;
        foreach (var t in FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null) t.font = font;
        }
    }

    private void ConfirmPlayerSelection(int pIdx)
    {
        if (isPlayerConfirmed[pIdx]) return;

        isPlayerConfirmed[pIdx] = true;
        selectedCharacters[pIdx] = availableCharacters[playerCursorIndices[pIdx]];

        if (playerListView != null)
        {
            playerListView.SetPlayerSelection(pIdx, selectedCharacters[pIdx]);
        }

        Debug.Log($"プレイヤー{pIdx + 1} が {selectedCharacters[pIdx].displayName} を確定");

        // 全員決定チェック(参加者のみ)
        bool allDone = true;
        for (int i = 0; i < 4; i++)
        {
            if (participating[i] && !isPlayerConfirmed[i]) { allDone = false; break; }
        }
        if (allDone)
        {
            OnAllPlayersSelected();
        }
    }

    private void UpdateUI()
    {
        // カード背景の色枠(旧ボタン選択方式の名残)は表示しない。位置はカーソルで示す。

        // スポットライト更新
        // 表示対象の決定: 
        // 1. 数字キーで指定されているプレイヤー
        // 2. 指定がない場合、最後に操作したプレイヤー
        // 3. そのプレイヤーが確定済みなら、最初の未確定プレイヤーを探す
        int displayIdx = (debugActivePlayer > 0) ? (debugActivePlayer - 1) : lastInputPlayerIndex;
        
        if (isPlayerConfirmed[displayIdx] && debugActivePlayer == 0)
        {
            // 最後に操作したプレイヤーが確定済みなら、まだ選んでいない参加者を見せる
            for (int i = 0; i < 4; i++)
            {
                if (participating[i] && !isPlayerConfirmed[i])
                {
                    displayIdx = i;
                    break;
                }
            }
        }

        CharacterData character = availableCharacters[playerCursorIndices[displayIdx]];
        if (spotlightView != null)
        {
            spotlightView.ShowCharacter(character, playerCursorIndices[displayIdx]);
            spotlightView.SetCurrentPlayer(displayIdx + 1);
        }

        UpdateGridVisuals();
    }

    private void UpdateGridVisuals()
    {
        if (gridView == null) return;
        for (int i = 0; i < gridView.characterCards.Length; i++)
        {
            var card = gridView.characterCards[i];
            if (card == null) continue;
            
            // 重複選択を許可するため、誰かが選んでいても暗くしない
            if (card.characterImage != null)
            {
                card.characterImage.color = Color.white;
            }
        }
    }

    private void OnAllPlayersSelected()
    {
        Debug.Log("全プレイヤーがキャラクターを選択しました");

        // GameSceneManagerにパーティ情報を保存(参加者のみ。非参加スロットは null)
        if (GameSceneManager.Instance != null)
        {
            var gsm = GameSceneManager.Instance;
            CharacterData first = null;
            for (int i = 0; i < gsm.selectedParty.Length; i++)
            {
                CharacterData c = (i < selectedCharacters.Length && participating[i]) ? selectedCharacters[i] : null;
                gsm.selectedParty[i] = c;
                if (first == null && c != null) first = c;
            }
            gsm.selectedCharacter = first; // 1人目 = 最初の参加者
        }

        // バトルシーンへ遷移
        Invoke(nameof(LoadBattleScene), 1f);
    }

    private void LoadBattleScene()
    {
        // GameSceneManagerに保存されたステージデータを使ってバトルへ
        if (GameSceneManager.Instance != null && GameSceneManager.Instance.selectedStage != null)
        {
            GameSceneManager.Instance.GoToBattle(GameSceneManager.Instance.selectedStage, 0);
        }
        else
        {
            Debug.LogWarning("ステージデータが見つかりません。ワールドマップへ戻ります。");
            GameSceneManager.Instance?.LoadWorldMapScene();
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void CreateDefaultCharacters()
    {
        // Player.pngからスプライトをロード
        Sprite[] playerSprites = LoadPlayerSprites();

        // デフォルトの8キャラクター作成
        var characters = new[]
        {
            new { name = "イルカ", role = CharacterRole.BasicAttacker, desc1 = "攻撃とバフが主軸", desc2 = "基本的な攻撃スタイル", color = new Color(0.3f, 0.7f, 1f), spriteIndex = 0 },
            new { name = "ネコ", role = CharacterRole.BasicDefender, desc1 = "防御とバフが主軸", desc2 = "味方を守る戦術", color = new Color(0.9f, 0.4f, 0.7f), spriteIndex = 1 },
            new { name = "ヒツジ", role = CharacterRole.BasicHealer, desc1 = "回復が主軸", desc2 = "仲間をサポート", color = new Color(0.5f, 0.6f, 1f), spriteIndex = 2 },
            new { name = "ニシカワ", role = CharacterRole.DeckBreaker, desc1 = "デッキ破壊特殊", desc2 = "全カード使用", color = new Color(1f, 0.9f, 0.3f), spriteIndex = 3 },
            new { name = "ラーテル", role = CharacterRole.SpecialAttacker, desc1 = "ハイリスク攻撃", desc2 = "攻撃カード主軸", color = new Color(0.8f, 0.9f, 0.4f), spriteIndex = 4 },
            new { name = "ピエロ", role = CharacterRole.SpecialDefender, desc1 = "特殊防御", desc2 = "防御+特殊カード", color = new Color(0.4f, 0.9f, 0.9f), spriteIndex = 5 },
            new { name = "アクマ", role = CharacterRole.SpecialHealer, desc1 = "特殊回復", desc2 = "回復+バフカード", color = new Color(0.7f, 0.3f, 0.3f), spriteIndex = 6 },
            new { name = "スガク先生", role = CharacterRole.RandomMaster, desc1 = "ランダム要素", desc2 = "特殊カード担当", color = new Color(0.9f, 0.7f, 0.5f), spriteIndex = 7 }
        };

        foreach (var c in characters)
        {
            var charData = ScriptableObject.CreateInstance<CharacterData>();
            charData.displayName = c.name;
            charData.role = c.role;
            charData.description1 = c.desc1;
            charData.description2 = c.desc2;
            charData.themeColor = c.color;

            // Player.pngスプライトシートから該当スプライトを取得
            if (playerSprites != null && c.spriteIndex < playerSprites.Length)
            {
                charData.characterSprite = playerSprites[c.spriteIndex];
            }
            else
            {
                Debug.LogWarning($"Sprite not found for {c.name} at index {c.spriteIndex}");
                charData.characterSprite = CreatePlaceholderSprite(c.color);
            }

            availableCharacters.Add(charData);
        }
    }

    private Sprite[] LoadPlayerSprites()
    {
        #if UNITY_EDITOR
        // エディタではAssetDatabaseから直接ロード
        Texture2D playerTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/Player.png");
        if (playerTexture != null)
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(playerTexture);
            Sprite[] sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();
            return sprites;
        }
        #endif

        return null;
    }

    private Sprite CreatePlaceholderSprite(Color themeColor)
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color bgColor = themeColor * 0.6f;
        bgColor.a = 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = 1f - (y / (float)size * 0.3f);
                Color pixelColor = bgColor * t;
                pixelColor.a = 1f;

                if (x < 2 || x >= size - 2 || y < 2 || y >= size - 2)
                {
                    pixelColor = themeColor;
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy()
    {
    }
}
