using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン間の遷移を管理するシングルトン。
/// シーン名の定義と遷移メソッドを提供します。
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
