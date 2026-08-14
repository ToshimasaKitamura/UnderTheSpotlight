using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン間の遷移を管理するシングルトン。
/// シーン名の定義と遷移メソッドを提供します。
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    private static GameSceneManager _instance;

    /// <summary>
    /// シングルトン。存在しなければ自動生成する(バトルシーンを直接再生した時などでも
    /// ゲームオーバー遷移が機能するように)。
    /// </summary>
    public static GameSceneManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameSceneManager>();
                if (_instance == null)
                {
                    var go = new GameObject("GameSceneManager");
                    _instance = go.AddComponent<GameSceneManager>();   // Awake で _instance/DontDestroyOnLoad 設定
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// シーン名の定数定義
    /// </summary>
    public static class Scenes
    {
        public const string Title = "TitleScene";
        public const string CharacterSelect = "CharacterSelectScene";
        public const string WorldMap = "WorldMapScene";
        public const string Battle = "BattleScene";
        public const string Result = "ResultScene";
    }

    /// <summary>
    /// シーン間で引き継ぐデータ
    /// </summary>
    public StageData selectedStage;
    public CharacterData selectedCharacter; // 1人目のキャラクター（後方互換性のため保持）
    public CharacterData[] selectedParty = new CharacterData[4]; // 選択された4人のキャラクター
    public int bossIndex;
    public bool isVictory;

    /// <summary>
    /// ステージセレクト画面(ロビー)で参加したプレイヤー。index=プレイヤー番号(0=P1..3=P4)。
    /// ローカル/ネット問わずここで募集し、以降のキャラ選択・バトルへ引き継ぐ。
    /// 何も参加していない(全 false)場合は「未募集」= 直接シーン再生とみなす。
    /// </summary>
    public bool[] joinedPlayers = new bool[4];

    /// <summary>参加人数(1〜4)。0 は未募集。</summary>
    public int JoinedCount
    {
        get
        {
            int c = 0;
            for (int i = 0; i < joinedPlayers.Length; i++) if (joinedPlayers[i]) c++;
            return c;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// タイトル画面へ遷移
    /// </summary>
    public void GoToTitle()
    {
        SceneTransitionManager.Instance.LoadSceneWithFade(Scenes.Title);
    }

    /// <summary>
    /// タイトル画面へ遷移（互換性のため）
    /// </summary>
    public void LoadTitleScene()
    {
        GoToTitle();
    }

    /// <summary>
    /// キャラクター選択画面へ遷移
    /// </summary>
    public void GoToCharacterSelect()
    {
        SceneTransitionManager.Instance.LoadSceneWithSlide(Scenes.CharacterSelect);
    }

    /// <summary>
    /// キャラクター選択画面へ遷移（互換性のため）
    /// </summary>
    public void LoadCharacterSelectScene()
    {
        GoToCharacterSelect();
    }

    /// <summary>
    /// ワールドマップへ遷移
    /// </summary>
    public void GoToWorldMap()
    {
        SceneTransitionManager.Instance.LoadSceneWithFade(Scenes.WorldMap);
    }

    /// <summary>
    /// ワールドマップへ遷移（互換性のため）
    /// </summary>
    public void LoadWorldMapScene()
    {
        GoToWorldMap();
    }

    /// <summary>
    /// バトルシーンへ遷移
    /// </summary>
    public void GoToBattle(StageData stage, int bossIdx = 0)
    {
        selectedStage = stage;
        bossIndex = bossIdx;
        SceneTransitionManager.Instance.LoadSceneWithWipe(Scenes.Battle);
    }

    /// <summary>
    /// リザルトシーンへ遷移
    /// </summary>
    public void GoToResult(bool victory)
    {
        isVictory = victory;
        SceneTransitionManager.Instance.LoadSceneWithFade(Scenes.Result);
    }
}
