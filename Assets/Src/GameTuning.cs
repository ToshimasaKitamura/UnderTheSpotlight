using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// ゲームバランス数値を1か所に集約した ScriptableObject。
/// Excel→CSV→Editor 取込で更新できる「数値の唯一の真実」。
/// 各 Phase は自身に [SerializeField] GameTuning tuning を持ち、Awake で ApplyTuning して
/// ローカル既定値を上書きする(tuning が null ならローカル値=Inspector 直接設定 を使う)。
///
/// customParams: プランナーが CSV で任意の key/value/description 行を追加できる。
/// コード側は GetFloat/GetInt/GetString/GetBool で参照する。
/// 例:
///   var sp = tuning.GetFloat("moveStartXSpeed", 5f);
/// </summary>
[CreateAssetMenu(fileName = "GameTuning", menuName = "UnderTheSpotlight/GameTuning")]
public class GameTuning : ScriptableObject
{
    [System.Serializable]
    public class CustomParam
    {
        [Tooltip("コードから参照する識別子。")]
        public string key;
        [Tooltip("値(数値/真偽/文字列いずれも文字列で持つ)。")]
        public string value;
        [TextArea(2, 3)]
        [Tooltip("プランナー用の説明文。【...○】の○が value に対応。")]
        public string description;
    }

    [Header("任意パラメータ(プランナー追加可)")]
    [Tooltip("CSV の未知 key はここに格納される。コードは GetFloat/Int/String/Bool で参照。")]
    public List<CustomParam> customParams = new List<CustomParam>();

    public string GetString(string key, string fallback = "")
    {
        if (customParams == null) return fallback;
        for (int i = 0; i < customParams.Count; i++)
            if (customParams[i] != null && customParams[i].key == key) return customParams[i].value ?? fallback;
        return fallback;
    }
    public float GetFloat(string key, float fallback = 0f)
    {
        var s = GetString(key, null);
        if (s == null) return fallback;
        return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
    public int GetInt(string key, int fallback = 0)
    {
        var s = GetString(key, null);
        if (s == null) return fallback;
        return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
    public bool GetBool(string key, bool fallback = false)
    {
        var s = GetString(key, null);
        if (s == null) return fallback;
        s = s.Trim().ToLowerInvariant();
        if (s == "1" || s == "true" || s == "yes" || s == "y") return true;
        if (s == "0" || s == "false" || s == "no" || s == "n") return false;
        return fallback;
    }

    /// <summary>CustomParam の追加/更新(CsvImporter から使用)。</summary>
    public void SetCustom(string key, string value, string description)
    {
        if (customParams == null) customParams = new List<CustomParam>();
        for (int i = 0; i < customParams.Count; i++)
        {
            if (customParams[i] != null && customParams[i].key == key)
            {
                customParams[i].value = value;
                if (!string.IsNullOrEmpty(description)) customParams[i].description = description;
                return;
            }
        }
        customParams.Add(new CustomParam { key = key, value = value, description = description });
    }


    [Header("プレイヤー / HP")]
    public int playerCount = 4;
    public int maxHp = 5;
    [Tooltip("各プレイヤーの色 #RRGGBB or #RRGGBBAA。長さ=playerCount。")]
    public string[] playerColors = new[] { "#FF5959", "#5C99FF", "#5CFF66", "#FFD959" };
    [Tooltip("プレイヤー切替時のセリフ。長さ=playerCount。")]
    public string[] playerCallouts = new[]
    {
        "1番さん出番ですッ！",
        "2番さん助けて〜！！",
        "3番さん任せたっ！",
        "4番さんお願いっ！"
    };

    [Header("CardSelect")]
    public int drawCount = 3;
    public float selectTimeLimit = 15f;
    public bool randomizeTurnOrder = false;

    [Header("ActionCommand")]
    public int roundsPerActionCommand = 3;
    public float baseTimeLimit = 4.5f;
    public float timeLimitPerRound = -0.7f;
    public float speedRampPerRound = 0.18f;
    public int buttonCountBase = 2;
    public float difficultyToSpeed = 0.03f;
    public float difficultyToInterfere = 0.012f;
    public float minTimeLimit = 1.6f;
    public float interRoundDelay = 0.7f;
    public float resultFlashDuration = 0.9f;

    [Header("Dodge")]
    public float playerTurnDuration = 4f;

    [Header("フォント(プロジェクト全体)")]
    [Tooltip("装飾的なフォント(タイトル / ヘッダ / 大きな数字)。未指定なら OS の明朝系を自動採用。")]
    public Font displayFont;
    [Tooltip("本文フォント(ラベル / 説明 / ヒント)。未指定なら OS のゴシック系を自動採用。")]
    public Font bodyFont;

    /// <summary>hex 文字列配列 → Color[] へ変換(失敗時は白)。</summary>
    public Color[] ResolvePlayerColors()
    {
        if (playerColors == null) return new Color[0];
        var arr = new Color[playerColors.Length];
        for (int i = 0; i < playerColors.Length; i++)
        {
            if (!ColorUtility.TryParseHtmlString(playerColors[i], out arr[i])) arr[i] = Color.white;
        }
        return arr;
    }
}
