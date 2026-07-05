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
    private CharacterData[] selectedCharacters;
    private List<int> availableIndices = new List<int>();
    
    // デバッグ用: 現在操作中のプレイヤー（0=自動/全, 1-4=固定）
    private int debugActivePlayer = 0;
    private int lastInputPlayerIndex = 0;

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
        }

        if (playerListView != null)
        {
            playerListView.ClearAll();
        }
        
        UpdateUI();
    }

    void Update()
    {
        if (inputCooldown > 0)
        {
            inputCooldown -= Time.deltaTime;
        }

        HandleInput();
    }

    private void HandleInput()
    {
        // 数字キーによるデバッグプレイヤー切り替え
        debugActivePlayer = 0;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.isPressed) debugActivePlayer = 1;
            else if (Keyboard.current.digit2Key.isPressed) debugActivePlayer = 2;
            else if (Keyboard.current.digit3Key.isPressed) debugActivePlayer = 3;
            else if (Keyboard.current.digit4Key.isPressed) debugActivePlayer = 4;
        }

        // 操作対象の決定
        // デバッグモード時は指定プレイヤーのみ、通常時は(暫定的に)P1のみ操作可能とするか、
        // あるいは全未確定プレイヤーを同時操作するか
        // ユーザーの要望は「数字キーを押しながらだとそのプレイヤーのカーソルを操作」
        
        if (debugActivePlayer > 0)
        {
            int pIdx = debugActivePlayer - 1;
            if (!isPlayerConfirmed[pIdx])
            {
                ProcessPlayerInput(pIdx);
            }
        }
        else
        {
            // デバッグキーなし：最後に操作したプレイヤーのみ操作
            // （そのプレイヤーが確定済みなら最初の未確定プレイヤーを操作）
            int targetPlayer = lastInputPlayerIndex;
            if (isPlayerConfirmed[targetPlayer])
            {
                // 最初の未確定プレイヤーを探す
                for (int i = 0; i < 4; i++)
                {
                    if (!isPlayerConfirmed[i])
                    {
                        targetPlayer = i;
                        break;
                    }
                }
            }

            if (!isPlayerConfirmed[targetPlayer])
            {
                ProcessPlayerInput(targetPlayer);
            }
        }

        // キャンセル（一律）
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            GameSceneManager.Instance?.LoadWorldMapScene();
        }
    }

    private void ProcessPlayerInput(int pIdx)
    {
        if (inputCooldown > 0) return;

        int moveX = 0;
        int moveY = 0;
        bool confirmPressed = false;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) moveX = 1;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) moveX = -1;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) moveY = -1;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) moveY = 1;

            confirmPressed = keyboard.enterKey.wasPressedThisFrame ||
                           keyboard.spaceKey.wasPressedThisFrame ||
                           keyboard.zKey.wasPressedThisFrame;
        }

        // 移動
        if (moveX != 0 || moveY != 0)
        {
            int delta = moveX != 0 ? moveX : moveY;
            int count = availableCharacters.Count;
            playerCursorIndices[pIdx] = (playerCursorIndices[pIdx] + delta + count) % count;
            
            lastInputPlayerIndex = pIdx;
            PlaySound(cursorMoveSfx);
            inputCooldown = INPUT_COOLDOWN_TIME;
            UpdateUI();
        }

        // 決定
        if (confirmPressed)
        {
            lastInputPlayerIndex = pIdx;
            ConfirmPlayerSelection(pIdx);
            PlaySound(confirmSfx);
            inputCooldown = INPUT_COOLDOWN_TIME;
            UpdateUI();
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

        // 全員決定チェック
        if (isPlayerConfirmed.All(c => c))
        {
            OnAllPlayersSelected();
        }
    }

    private void UpdateUI()
    {
        // グリッドの枠表示更新
        if (gridView != null)
        {
            gridView.SetPlayerCursors(playerCursorIndices);
        }

        // スポットライト更新
        // 表示対象の決定: 
        // 1. 数字キーで指定されているプレイヤー
        // 2. 指定がない場合、最後に操作したプレイヤー
        // 3. そのプレイヤーが確定済みなら、最初の未確定プレイヤーを探す
        int displayIdx = (debugActivePlayer > 0) ? (debugActivePlayer - 1) : lastInputPlayerIndex;
        
        if (isPlayerConfirmed[displayIdx] && debugActivePlayer == 0)
        {
            // 最後に操作したプレイヤーが確定済みなら、まだ選んでいない人を見せる
            for (int i = 0; i < 4; i++)
            {
                if (!isPlayerConfirmed[i])
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

        // GameSceneManagerにパーティ情報を保存
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.selectedCharacter = selectedCharacters[0];
            for (int i = 0; i < selectedCharacters.Length; i++)
            {
                if (i < GameSceneManager.Instance.selectedParty.Length)
                {
                    GameSceneManager.Instance.selectedParty[i] = selectedCharacters[i];
                }
            }
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
