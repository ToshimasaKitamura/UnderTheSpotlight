using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// キャラクター選択シーンの仮実装（8bitドット絵風・IMGUI）。
///
/// 本来の CharacterSelectScene は uGUI 前提の凝った実装でしたが、シーン内のスクリプト参照が
/// すべて欠落(Missing Script)して機能していなかったため、タイトル/リザルトと同じ描画スタイルで
/// 「パーティを選んでバトルへ進む」最低限の導線を仮実装します。
///
/// 参加メンバー(selectedParty)は現状バトルの進行には影響しませんが、将来の利用に備えて設定します。
/// </summary>
public class TempCharacterSelectController : MonoBehaviour
{
    [Header("キャラクターデータ")]
    [Tooltip("選択候補。シーンで Assets/Data/Characters の各アセットを割り当て。")]
    public List<CharacterData> availableCharacters = new List<CharacterData>();

    [Header("設定")]
    public int partySize = 4;

    // 色はテーマ(GameColorScheme)に一元化。正典: Docs/Specifications/デザインテーマ.md

    private readonly List<int> selected = new List<int>();

    private float inputDelay = 0.5f;
    private float elapsedTime;
    private bool canAcceptInput;

    private Texture2D bgTexture;
    private float starTimer;
    private Vector2[] starPositions;
    private float[] starPhases;
    private int pixelScale = 3;

    private void Start()
    {
        availableCharacters.RemoveAll(c => c == null);
        if (availableCharacters.Count == 0)
        {
            Debug.LogError("[TempCharacterSelect] availableCharacters が空です。" +
                "シーンの CharacterSelectController に Assets/Data/Characters のキャラクターアセットを割り当ててください。");
        }

        pixelScale = Mathf.Max(2, Screen.height / 360);
        elapsedTime = 0f;
        canAcceptInput = false;

        bgTexture = PixelFontRenderer.CreateDitheredGradient(
            Screen.width / 4, Screen.height / 4,
            GameColorScheme.BackgroundTop, GameColorScheme.BackgroundBottom, 2);
        InitializeStars();
    }

    private void OnDestroy()
    {
        if (bgTexture != null) Destroy(bgTexture);
    }

    private void InitializeStars()
    {
        int starCount = 24;
        starPositions = new Vector2[starCount];
        starPhases = new float[starCount];
        System.Random rng = new System.Random(55);
        for (int i = 0; i < starCount; i++)
        {
            starPositions[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
            starPhases[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
        }
    }

    private void Update()
    {
        starTimer += Time.deltaTime;
        if (!canAcceptInput)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= inputDelay) canAcceptInput = true;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) GoBack();
    }

    private void ToggleSelect(int index)
    {
        if (selected.Contains(index)) selected.Remove(index);
        else if (selected.Count < partySize) selected.Add(index);
    }

    private void StartBattle()
    {
        var gm = GameSceneManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[TempCharacterSelect] GameSceneManager.Instance が取得できず、バトルへ遷移できません。");
            return;
        }

        // 選択メンバー(不足分は先頭から補完)で party を構築。バトル自体は現状 party 非依存だが将来用に設定。
        var party = new List<CharacterData>();
        foreach (var idx in selected) party.Add(availableCharacters[idx]);
        for (int i = 0; i < availableCharacters.Count && party.Count < partySize; i++)
        {
            if (!party.Contains(availableCharacters[i])) party.Add(availableCharacters[i]);
        }

        for (int i = 0; i < gm.selectedParty.Length; i++)
            gm.selectedParty[i] = (i < party.Count) ? party[i] : null;
        if (party.Count > 0) gm.selectedCharacter = party[0];

        gm.GoToBattle(gm.selectedStage, gm.bossIndex);
    }

    private void GoBack()
    {
        GameSceneManager.Instance?.GoToWorldMap();
    }

    private void OnGUI()
    {
        // 背景
        if (bgTexture != null)
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTexture, ScaleMode.StretchToFill);
        DrawStars();

        float cx = Screen.width / 2f;

        // タイトル(日本語は misaki フォントの Label)
        GUI.Label(new Rect(0, Screen.height * 0.06f, Screen.width, Screen.height * 0.1f),
            "キャラクター選択（仮）", TitleStyle());
        GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, Screen.height * 0.06f),
            $"参加メンバーを選んでください（{selected.Count}/{partySize}）", SubtitleStyle());

        if (!canAcceptInput) return;

        // キャラクター選択グリッド
        var btn = ButtonStyle();
        var sel = SelectedButtonStyle();
        int cols = 4;
        float gridW = Mathf.Min(Screen.width * 0.9f, 880f);
        float cellW = (gridW - (cols - 1) * 12f) / cols;
        float cellH = Mathf.Max(52f, Screen.height * 0.09f);
        float startX = cx - gridW / 2f;
        float startY = Screen.height * 0.26f;

        for (int i = 0; i < availableCharacters.Count; i++)
        {
            int row = i / cols, col = i % cols;
            var rect = new Rect(startX + col * (cellW + 12f), startY + row * (cellH + 12f), cellW, cellH);
            bool isSel = selected.Contains(i);
            string label = DisplayName(availableCharacters[i]);
            if (isSel) label = "★ " + label;
            if (GUI.Button(rect, label, isSel ? sel : btn)) ToggleSelect(i);
        }

        // 下部ボタン
        float bw = Mathf.Min(Screen.width * 0.4f, 320f);
        float bh = Mathf.Max(50f, Screen.height * 0.08f);
        float by = Screen.height * 0.84f;
        if (GUI.Button(new Rect(cx - bw - 10f, by, bw, bh), "MAPに戻る", btn)) GoBack();
        if (GUI.Button(new Rect(cx + 10f, by, bw, bh), "バトル開始", btn)) StartBattle();
    }

    private void DrawStars()
    {
        if (starPositions == null) return;
        for (int i = 0; i < starPositions.Length; i++)
        {
            float twinkle = (Mathf.Sin(starTimer * 2f + starPhases[i]) + 1f) * 0.5f;
            if (twinkle < 0.3f) continue;
            float x = starPositions[i].x * Screen.width;
            float y = starPositions[i].y * Screen.height;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 0.9f, twinkle * 0.5f);
            GUI.DrawTexture(new Rect(x, y, pixelScale, pixelScale), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }

    private static string DisplayName(CharacterData c)
    {
        if (c == null) return "---";
        return !string.IsNullOrEmpty(c.displayName) ? c.displayName : c.name;
    }

    private GUIStyle cachedTitle, cachedSubtitle, cachedBtn, cachedSelBtn;

    private GUIStyle TitleStyle()
    {
        if (cachedTitle == null)
        {
            cachedTitle = new GUIStyle(GUI.skin.label);
            var f = GameFont.Body();
            if (f != null) cachedTitle.font = f;
            cachedTitle.fontSize = Mathf.Max(24, pixelScale * 10);
            cachedTitle.fontStyle = FontStyle.Bold;
            cachedTitle.alignment = TextAnchor.MiddleCenter;
            cachedTitle.normal.textColor = GameColorScheme.Accent;
        }
        return cachedTitle;
    }

    private GUIStyle SubtitleStyle()
    {
        if (cachedSubtitle == null)
        {
            cachedSubtitle = new GUIStyle(GUI.skin.label);
            var f = GameFont.Body();
            if (f != null) cachedSubtitle.font = f;
            cachedSubtitle.fontSize = Mathf.Max(14, pixelScale * 6);
            cachedSubtitle.alignment = TextAnchor.MiddleCenter;
            cachedSubtitle.normal.textColor = GameColorScheme.TextMuted;
        }
        return cachedSubtitle;
    }

    private GUIStyle ButtonStyle()
    {
        if (cachedBtn == null)
        {
            cachedBtn = new GUIStyle(GUI.skin.button);
            var f = GameFont.Body();
            if (f != null) cachedBtn.font = f;
            cachedBtn.fontSize = Mathf.Max(16, pixelScale * 6);
            cachedBtn.fontStyle = FontStyle.Bold;
        }
        return cachedBtn;
    }

    private GUIStyle SelectedButtonStyle()
    {
        if (cachedSelBtn == null)
        {
            cachedSelBtn = new GUIStyle(ButtonStyle());
            cachedSelBtn.normal.textColor = GameColorScheme.Accent;
            cachedSelBtn.hover.textColor = GameColorScheme.Accent;
        }
        return cachedSelBtn;
    }
}
