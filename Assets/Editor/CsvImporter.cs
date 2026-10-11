#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Excel / スプレッドシートから書き出した CSV を読み、Card / Deck / GameTuning / Enemy の ScriptableObject を自動生成・更新する。
/// メニュー: UnderTheSpotlight > Reimport from CSV (Excel読み込み・カード自動生成)
/// 既定パス: Assets/Data/csv/{cards.csv, decks.csv, game_tuning.csv, enemy_parts.csv}
/// CSV はカンマ区切り・UTF-8(BOM 可)・セル内カンマは "..."で囲み、内部の二重引用符は "" でエスケープ。
/// </summary>
public static class CsvImporter
{
    private const string CsvDir = "Assets/Data/csv";
    private const string CardsCsv = "cards.csv";
    private const string DecksCsv = "decks.csv";
    private const string TuningCsv = "game_tuning.csv";
    private const string EnemyPartsCsv = "enemy_parts.csv";

    private const string CardAssetDir = "Assets/Resources/Data/Cards";
    private const string DeckAssetDir = "Assets/Resources/Data";
    private const string EnemyAssetDir = "Assets/Data/Enemies";
    private const string TuningAssetPath = "Assets/Resources/Data/GameTuning.asset";

    [MenuItem("UnderTheSpotlight/Reimport from CSV (Excel読み込み・カード自動生成)", priority = 100)]
    public static void ReimportAll()
    {
        string summary = ReimportAllSilent();
        EditorUtility.DisplayDialog("CSV Reimport",
            "CSVからカードと設定を自動生成・更新しました！\n\n" + summary, "OK");
    }

    /// <summary>
    /// ダイアログを出さずに全 CSV を反映する(CSV 保存時の自動インポートから呼ぶ)。
    /// 返り値は件数サマリ文字列。
    /// </summary>
    public static string ReimportAllSilent()
    {
        int cards = ReimportCards();
        int decks = ReimportDecks();
        bool tuning = ReimportTuning();
        int enemies = ReimportEnemies();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string summary = $"カード {cards} 件 / デッキ {decks} 件 / GameTuning {(tuning ? "更新" : "スキップ")} / 敵部位 {enemies} 件";
        Debug.Log($"[CsvImporter] CSV インポート完了: {summary}");
        return summary;
    }

    [MenuItem("UnderTheSpotlight/Export Current to CSV (Excel用に書き出し)", priority = 101)]
    public static void ExportAll()
    {
        Directory.CreateDirectory(CsvDir);
        ExportCards();
        ExportDecks();
        ExportTuning();
        ExportEnemies();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("CSV Export", $"現在の設定をCSVに出力しました。\nExcelで編集可能です。\n\n出力先: {CsvDir}", "OK");
    }

    // ------------ Cards ------------
    private static readonly string[] CardHeader = {
        "id","displayName","description","category","tier",
        "baseDifficultyModifier","basePower","difficultyPerUpgrade","powerPerUpgrade",
        "effectType","attackRange","cardColor"
    };

    public static int ReimportCards()
    {
        string path = Path.Combine(CsvDir, CardsCsv);
        if (!File.Exists(path)) { Debug.LogWarning($"[CsvImporter] not found: {path}"); return 0; }
        var rows = ReadCsv(path);
        if (rows.Count < 2) return 0;

        // 既存 CardData を id → asset でインデックス化
        var existing = new Dictionary<string, CardData>();
        foreach (var guid in AssetDatabase.FindAssets("t:CardData"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<CardData>(p);
            if (c != null && !string.IsNullOrEmpty(c.id)) existing[c.id] = c;
        }

        var header = rows[0];
        var col = MakeIndex(header);
        if (!Directory.Exists(CardAssetDir)) Directory.CreateDirectory(CardAssetDir);

        int count = 0;
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string id = Get(row, col, "id");
            if (string.IsNullOrWhiteSpace(id) || id.StartsWith("#")) continue; // コメント行スキップ

            CardData card;
            if (!existing.TryGetValue(id, out card) || card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                string assetPath = $"{CardAssetDir}/{id}.asset";
                AssetDatabase.CreateAsset(card, assetPath);
                existing[id] = card;
            }

            card.id = id;
            card.displayName = Get(row, col, "displayName");
            card.description = Get(row, col, "description");
            card.category = ParseCategory(Get(row, col, "category"));
            card.tier = ParseTier(Get(row, col, "tier"));
            card.baseDifficultyModifier = ParseInt(Get(row, col, "baseDifficultyModifier"), 0);
            card.basePower = ParseInt(Get(row, col, "basePower"), 1);
            card.difficultyPerUpgrade = ParseInt(Get(row, col, "difficultyPerUpgrade"), 1);
            card.powerPerUpgrade = ParseInt(Get(row, col, "powerPerUpgrade"), 1);
            card.effectType = ParseEffectType(Get(row, col, "effectType"));
            
            string arRaw = Get(row, col, "attackRange");
            if (string.IsNullOrWhiteSpace(arRaw))
                card.attackRange = card.category == CardCategory.Attack ? CardAttackRange.Single : CardAttackRange.All;
            else
                card.attackRange = ParseAttackRange(arRaw, card.category);

            card.cardColor = ParseColor(Get(row, col, "cardColor"), card.category);
            EditorUtility.SetDirty(card);
            count++;
        }
        return count;
    }

    public static void ExportCards()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", CardHeader));
        foreach (var guid in AssetDatabase.FindAssets("t:CardData"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<CardData>(p);
            if (c == null) continue;
            sb.AppendLine(string.Join(",",
                Quote(c.id), Quote(c.displayName), Quote(c.description),
                CategoryToString(c.category), TierToString(c.tier),
                c.baseDifficultyModifier.ToString(), c.basePower.ToString(),
                c.difficultyPerUpgrade.ToString(), c.powerPerUpgrade.ToString(),
                EffectTypeToString(c.effectType),
                AttackRangeToString(c.attackRange),
                Quote(ColorToHex(c.cardColor))));
        }
        WriteUtf8Bom(Path.Combine(CsvDir, CardsCsv), sb.ToString());
    }

    // ------------ Enum Parsing with Japanese support ------------
    private static CardCategory ParseCategory(string s)
    {
        s = (s ?? "").Trim();
        if (s == "0" || s.Equals("Attack", StringComparison.OrdinalIgnoreCase) || s == "攻撃" || s == "アタック") return CardCategory.Attack;
        if (s == "1" || s.Equals("Defend", StringComparison.OrdinalIgnoreCase) || s == "防御" || s == "ガード" || s == "ディフェンス") return CardCategory.Defend;
        if (s == "2" || s.Equals("Heal", StringComparison.OrdinalIgnoreCase) || s == "回復" || s == "ヒール") return CardCategory.Heal;
        if (s == "3" || s.Equals("Buff", StringComparison.OrdinalIgnoreCase) || s == "バフ" || s == "強化") return CardCategory.Buff;
        if (s == "4" || s.Equals("Special", StringComparison.OrdinalIgnoreCase) || s == "特殊" || s == "スペシャル") return CardCategory.Special;
        return CardCategory.Attack;
    }

    private static string CategoryToString(CardCategory cat)
    {
        switch (cat)
        {
            case CardCategory.Attack: return "攻撃";
            case CardCategory.Defend: return "防御";
            case CardCategory.Heal: return "回復";
            case CardCategory.Buff: return "バフ";
            case CardCategory.Special: return "特殊";
            default: return cat.ToString();
        }
    }

    private static CardTier ParseTier(string s)
    {
        s = (s ?? "").Trim();
        if (s == "0" || s.Equals("Basic", StringComparison.OrdinalIgnoreCase) || s == "基本" || s == "ノーマル") return CardTier.Basic;
        if (s == "1" || s.Equals("Special", StringComparison.OrdinalIgnoreCase) || s == "特殊" || s == "レア") return CardTier.Special;
        if (s == "2" || s.Equals("Ultimate", StringComparison.OrdinalIgnoreCase) || s == "必殺" || s == "フィナーレ" || s == "ウルトラ") return CardTier.Ultimate;
        return CardTier.Basic;
    }

    private static string TierToString(CardTier tier)
    {
        switch (tier)
        {
            case CardTier.Basic: return "基本";
            case CardTier.Special: return "特殊";
            case CardTier.Ultimate: return "必殺";
            default: return tier.ToString();
        }
    }

    private static CardAttackRange ParseAttackRange(string s, CardCategory category)
    {
        s = (s ?? "").Trim();
        if (s == "0" || s.Equals("Single", StringComparison.OrdinalIgnoreCase) || s == "単体" || s == "部位選択") return CardAttackRange.Single;
        if (s == "1" || s.Equals("Upper", StringComparison.OrdinalIgnoreCase) || s == "上半身" || s == "上部") return CardAttackRange.Upper;
        if (s == "2" || s.Equals("Lower", StringComparison.OrdinalIgnoreCase) || s == "下半身" || s == "下部") return CardAttackRange.Lower;
        if (s == "3" || s.Equals("All", StringComparison.OrdinalIgnoreCase) || s == "全体" || s == "全員") return CardAttackRange.All;
        return category == CardCategory.Attack ? CardAttackRange.Single : CardAttackRange.All;
    }

    private static string AttackRangeToString(CardAttackRange range)
    {
        switch (range)
        {
            case CardAttackRange.Single: return "単体";
            case CardAttackRange.Upper: return "上半身";
            case CardAttackRange.Lower: return "下半身";
            case CardAttackRange.All: return "全体";
            default: return range.ToString();
        }
    }

    private static CardEffectType ParseEffectType(string s)
    {
        s = (s ?? "").Trim();
        if (s == "0" || s.Equals("None", StringComparison.OrdinalIgnoreCase) || s == "なし") return CardEffectType.None;
        if (s == "1" || s.Equals("AttackNoHitBonus", StringComparison.OrdinalIgnoreCase) || s.Contains("ノーヒット") || s.Contains("大ダメージ")) return CardEffectType.AttackNoHitBonus;
        if (s == "2" || s.Equals("DefendTauntReduce", StringComparison.OrdinalIgnoreCase) || s.Contains("挑発") || s.Contains("軽減") || s.Contains("対象")) return CardEffectType.DefendTauntReduce;
        if (s == "3" || s.Equals("HealImmediateAndOnDodge", StringComparison.OrdinalIgnoreCase) || s.Contains("回復") || s.Contains("回避")) return CardEffectType.HealImmediateAndOnDodge;
        if (s == "4" || s.Equals("BuffGeneric", StringComparison.OrdinalIgnoreCase) || s.Contains("バフ") || s.Contains("上昇")) return CardEffectType.BuffGeneric;
        if (s == "5" || s.Equals("SpecialSuccessScaling", StringComparison.OrdinalIgnoreCase) || s.Contains("成功数") || s.Contains("アクションコマンド")) return CardEffectType.SpecialSuccessScaling;
        return ParseEnum(s, CardEffectType.None);
    }

    private static string EffectTypeToString(CardEffectType effect)
    {
        switch (effect)
        {
            case CardEffectType.None: return "None";
            case CardEffectType.AttackNoHitBonus: return "AttackNoHitBonus";
            case CardEffectType.DefendTauntReduce: return "DefendTauntReduce";
            case CardEffectType.HealImmediateAndOnDodge: return "HealImmediateAndOnDodge";
            case CardEffectType.BuffGeneric: return "BuffGeneric";
            case CardEffectType.SpecialSuccessScaling: return "SpecialSuccessScaling";
            default: return effect.ToString();
        }
    }

    private static Color ParseColor(string s, CardCategory category)
    {
        if (ColorUtility.TryParseHtmlString((s ?? "").Trim(), out var c)) return c;
        switch (category)
        {
            case CardCategory.Attack: return new Color(1f, 0.35f, 0.35f, 1f);
            case CardCategory.Defend: return new Color(0.36f, 0.6f, 1f, 1f);
            case CardCategory.Heal: return new Color(0.36f, 0.9f, 0.4f, 1f);
            case CardCategory.Buff: return new Color(0.8f, 0.5f, 1f, 1f);
            case CardCategory.Special: return new Color(1f, 0.85f, 0.35f, 1f);
            default: return Color.white;
        }
    }

    // ------------ Decks ------------
    private static readonly string[] DeckHeader = { "deckName", "cardId", "copies" };

    public static int ReimportDecks()
    {
        string path = Path.Combine(CsvDir, DecksCsv);
        if (!File.Exists(path)) { Debug.LogWarning($"[CsvImporter] not found: {path}"); return 0; }
        var rows = ReadCsv(path);
        if (rows.Count < 2) return 0;
        var col = MakeIndex(rows[0]);

        // id → CardData
        var cardsById = new Dictionary<string, CardData>();
        foreach (var guid in AssetDatabase.FindAssets("t:CardData"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<CardData>(p);
            if (c != null && !string.IsNullOrEmpty(c.id)) cardsById[c.id] = c;
        }

        // deckName → entries
        var bucket = new Dictionary<string, List<DeckDefinition.Entry>>();
        for (int r = 1; r < rows.Count; r++)
        {
            string deckName = Get(rows[r], col, "deckName");
            string cardId = Get(rows[r], col, "cardId");
            int copies = ParseInt(Get(rows[r], col, "copies"), 0);
            if (string.IsNullOrWhiteSpace(deckName) || string.IsNullOrWhiteSpace(cardId) || copies <= 0) continue;
            if (!cardsById.TryGetValue(cardId, out var card))
            {
                Debug.LogWarning($"[CsvImporter] deck '{deckName}' references unknown card id '{cardId}'");
                continue;
            }
            if (!bucket.TryGetValue(deckName, out var list)) { list = new List<DeckDefinition.Entry>(); bucket[deckName] = list; }
            list.Add(new DeckDefinition.Entry { card = card, copies = copies });
        }

        int count = 0;
        foreach (var kv in bucket)
        {
            string assetPath = $"{DeckAssetDir}/{kv.Key}.asset";
            var deck = AssetDatabase.LoadAssetAtPath<DeckDefinition>(assetPath);
            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<DeckDefinition>();
                AssetDatabase.CreateAsset(deck, assetPath);
            }
            deck.entries = kv.Value;
            EditorUtility.SetDirty(deck);
            count++;
        }
        return count;
    }

    public static void ExportDecks()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", DeckHeader));
        foreach (var guid in AssetDatabase.FindAssets("t:DeckDefinition"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var d = AssetDatabase.LoadAssetAtPath<DeckDefinition>(p);
            if (d == null) continue;
            string deckName = Path.GetFileNameWithoutExtension(p);
            foreach (var e in d.entries)
            {
                if (e.card == null) continue;
                sb.AppendLine(string.Join(",", Quote(deckName), Quote(e.card.id), e.copies.ToString()));
            }
        }
        WriteUtf8Bom(Path.Combine(CsvDir, DecksCsv), sb.ToString());
    }

    // ------------ Enemies (parts) ------------
    private static readonly string[] EnemyHeader = {
        "enemyId","enemyDisplayName","partId","partDisplayName","zone","maxHp","effect","hintCue",
        "hitboxCenterX","hitboxCenterY","hitboxSizeX","hitboxSizeY"
    };

    public static int ReimportEnemies()
    {
        string path = Path.Combine(CsvDir, EnemyPartsCsv);
        if (!File.Exists(path)) return 0;
        var rows = ReadCsv(path);
        if (rows.Count < 2) return 0;
        var col = MakeIndex(rows[0]);

        var bucket = new Dictionary<string, (string display, List<EnemyPart> parts)>();
        for (int r = 1; r < rows.Count; r++)
        {
            string enemyId = Get(rows[r], col, "enemyId");
            string enemyDisplay = Get(rows[r], col, "enemyDisplayName");
            string partId = Get(rows[r], col, "partId");
            if (string.IsNullOrWhiteSpace(enemyId) || string.IsNullOrWhiteSpace(partId)) continue;

            if (!bucket.TryGetValue(enemyId, out var entry))
            {
                entry = (enemyDisplay, new List<EnemyPart>());
                bucket[enemyId] = entry;
            }
            if (string.IsNullOrWhiteSpace(entry.display) && !string.IsNullOrWhiteSpace(enemyDisplay))
            {
                entry.display = enemyDisplay;
                bucket[enemyId] = entry;
            }

            var part = new EnemyPart
            {
                id = partId.Trim(),
                displayName = Get(rows[r], col, "partDisplayName"),
                zone = ParseEnum(Get(rows[r], col, "zone"), BodyZone.Upper),
                maxHp = ParseInt(Get(rows[r], col, "maxHp"), 3),
                destructionEffect = ParseEnum(Get(rows[r], col, "effect"), PartDestructionEffect.None),
                hintCue = Get(rows[r], col, "hintCue"),
                hitboxCenter = new Vector2(
                    ParseFloat(Get(rows[r], col, "hitboxCenterX"), 0.5f),
                    ParseFloat(Get(rows[r], col, "hitboxCenterY"), 0.5f)),
                hitboxSize = new Vector2(
                    ParseFloat(Get(rows[r], col, "hitboxSizeX"), 0.4f),
                    ParseFloat(Get(rows[r], col, "hitboxSizeY"), 0.3f)),
            };
            entry.parts.Add(part);
        }

        if (!Directory.Exists(EnemyAssetDir)) Directory.CreateDirectory(EnemyAssetDir);
        int count = 0;
        foreach (var kv in bucket)
        {
            string assetPath = $"{EnemyAssetDir}/{kv.Key}.asset";
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
            if (enemy == null)
            {
                enemy = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(enemy, assetPath);
            }
            enemy.id = kv.Key;
            if (!string.IsNullOrWhiteSpace(kv.Value.display)) enemy.displayName = kv.Value.display;
            enemy.parts = kv.Value.parts;
            EditorUtility.SetDirty(enemy);
            count++;
        }
        return count;
    }

    public static void ExportEnemies()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", EnemyHeader));
        foreach (var guid in AssetDatabase.FindAssets("t:EnemyData"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var e = AssetDatabase.LoadAssetAtPath<EnemyData>(p);
            if (e == null || e.parts == null) continue;
            foreach (var part in e.parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.id)) continue;
                sb.AppendLine(string.Join(",",
                    Quote(e.id), Quote(e.displayName),
                    Quote(part.id), Quote(part.displayName),
                    part.zone.ToString(),
                    part.maxHp.ToString(),
                    part.destructionEffect.ToString(),
                    Quote(part.hintCue ?? ""),
                    Inv(part.hitboxCenter.x), Inv(part.hitboxCenter.y),
                    Inv(part.hitboxSize.x), Inv(part.hitboxSize.y)));
            }
        }
        WriteUtf8Bom(Path.Combine(CsvDir, EnemyPartsCsv), sb.ToString());
    }

    // ------------ GameTuning ------------
    private static readonly string[] TuningHeader = { "key", "value", "description" };

    private static readonly Dictionary<string, string> KnownTuningDesc = new Dictionary<string, string>
    {
        { "playerCount", "プレイヤーは【○人】で戦う" },
        { "maxHp", "全員で共有するHPは最大【○】" },
        { "drawCount", "カード選択時、各プレイヤーは山札から【○枚】引く" },
        { "selectTimeLimit", "カード選択は1人あたり【○秒】以内に行う" },
        { "randomizeTurnOrder", "ターン順をランダム化するかどうか【true=ランダム / false=固定】" },
        { "roundsPerActionCommand", "アクションコマンドフェーズでは【○回】ミニゲームを行う" },
        { "baseTimeLimit", "ミニゲーム1回の基本制限時間は【○秒】" },
        { "timeLimitPerRound", "ラウンドが進むごとに制限時間が【○秒】ずつ増減する(負の値で短くなって難しくなる)" },
        { "speedRampPerRound", "ラウンドが進むごとに進行速度が【○】ずつ加算される" },
        { "buttonCountBase", "ミニゲームで要求される入力数の基本値は【○】" },
        { "difficultyToSpeed", "カードの難度合計1点につき、速度が【○】加算される" },
        { "difficultyToInterfere", "カードの難度合計1点につき、視覚妨害が【○】加算される" },
        { "minTimeLimit", "ミニゲームの制限時間は最低でも【○秒】は確保する" },
        { "interRoundDelay", "ラウンドとラウンドの間に【○秒】の小休止を挟む" },
        { "resultFlashDuration", "成功/失敗の表示は【○秒】間出す" },
        { "playerTurnDuration", "弾幕中、1人のプレイヤーが操作する時間は【○秒】" },
        { "playerColors", "各プレイヤーの色を【;区切りで列挙】 例: #FF5959;#5C99FF;#5CFF66;#FFD959" },
        { "playerCallouts", "プレイヤー切替時のセリフを【|区切りで列挙】 (1Pから順)" },
    };

    public static bool ReimportTuning()
    {
        string path = Path.Combine(CsvDir, TuningCsv);
        if (!File.Exists(path)) { Debug.LogWarning($"[CsvImporter] not found: {path}"); return false; }
        var rows = ReadCsv(path);
        if (rows.Count < 2) return false;
        var col = MakeIndex(rows[0]);

        var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningAssetPath);
        if (tuning == null)
        {
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            AssetDatabase.CreateAsset(tuning, TuningAssetPath);
        }

        if (tuning.customParams == null) tuning.customParams = new List<GameTuning.CustomParam>();
        tuning.customParams.Clear();

        for (int r = 1; r < rows.Count; r++)
        {
            string k = Get(rows[r], col, "key");
            string v = Get(rows[r], col, "value");
            string d = Get(rows[r], col, "description");
            if (string.IsNullOrWhiteSpace(k)) continue;
            k = k.Trim();
            if (!ApplyKnownTuning(tuning, k, v))
            {
                tuning.SetCustom(k, v, d);
            }
        }

        EditorUtility.SetDirty(tuning);
        return true;
    }

    private static bool ApplyKnownTuning(GameTuning t, string k, string s)
    {
        switch (k)
        {
            case "playerCount": t.playerCount = ParseInt(s, t.playerCount); return true;
            case "maxHp": t.maxHp = ParseInt(s, t.maxHp); return true;
            case "drawCount": t.drawCount = ParseInt(s, t.drawCount); return true;
            case "selectTimeLimit": t.selectTimeLimit = ParseFloat(s, t.selectTimeLimit); return true;
            case "randomizeTurnOrder": t.randomizeTurnOrder = ParseBool(s, t.randomizeTurnOrder); return true;
            case "roundsPerActionCommand": t.roundsPerActionCommand = ParseInt(s, t.roundsPerActionCommand); return true;
            case "baseTimeLimit": t.baseTimeLimit = ParseFloat(s, t.baseTimeLimit); return true;
            case "timeLimitPerRound": t.timeLimitPerRound = ParseFloat(s, t.timeLimitPerRound); return true;
            case "speedRampPerRound": t.speedRampPerRound = ParseFloat(s, t.speedRampPerRound); return true;
            case "buttonCountBase": t.buttonCountBase = ParseInt(s, t.buttonCountBase); return true;
            case "difficultyToSpeed": t.difficultyToSpeed = ParseFloat(s, t.difficultyToSpeed); return true;
            case "difficultyToInterfere": t.difficultyToInterfere = ParseFloat(s, t.difficultyToInterfere); return true;
            case "minTimeLimit": t.minTimeLimit = ParseFloat(s, t.minTimeLimit); return true;
            case "interRoundDelay": t.interRoundDelay = ParseFloat(s, t.interRoundDelay); return true;
            case "resultFlashDuration": t.resultFlashDuration = ParseFloat(s, t.resultFlashDuration); return true;
            case "playerTurnDuration": t.playerTurnDuration = ParseFloat(s, t.playerTurnDuration); return true;
            case "playerColors": t.playerColors = SplitList(s); return true;
            case "playerCallouts": t.playerCallouts = SplitList(s, '|'); return true;
            default:
                if (TryApplyIndexed(k, "playerColor_", s, ref t.playerColors)) return true;
                if (TryApplyIndexed(k, "playerCallout_", s, ref t.playerCallouts)) return true;
                return false;
        }
    }

    private static bool TryApplyIndexed(string key, string prefix, string value, ref string[] array)
    {
        if (!key.StartsWith(prefix)) return false;
        if (!int.TryParse(key.Substring(prefix.Length), out int idx1)) return false;
        if (idx1 < 1) return false;
        int idx0 = idx1 - 1;
        if (array == null) array = new string[0];
        if (idx0 >= array.Length)
        {
            var grown = new string[idx0 + 1];
            for (int i = 0; i < array.Length; i++) grown[i] = array[i];
            array = grown;
        }
        array[idx0] = value;
        return true;
    }

    public static void ExportTuning()
    {
        var t = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningAssetPath);
        if (t == null) return;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", TuningHeader));
        void Row(string k, string v) =>
            sb.AppendLine($"{Quote(k)},{Quote(v)},{Quote(KnownTuningDesc.TryGetValue(k, out var d) ? d : "")}");
        Row("playerCount", t.playerCount.ToString());
        Row("maxHp", t.maxHp.ToString());
        Row("drawCount", t.drawCount.ToString());
        Row("selectTimeLimit", Inv(t.selectTimeLimit));
        Row("randomizeTurnOrder", t.randomizeTurnOrder ? "true" : "false");
        Row("roundsPerActionCommand", t.roundsPerActionCommand.ToString());
        Row("baseTimeLimit", Inv(t.baseTimeLimit));
        Row("timeLimitPerRound", Inv(t.timeLimitPerRound));
        Row("speedRampPerRound", Inv(t.speedRampPerRound));
        Row("buttonCountBase", t.buttonCountBase.ToString());
        Row("difficultyToSpeed", Inv(t.difficultyToSpeed));
        Row("difficultyToInterfere", Inv(t.difficultyToInterfere));
        Row("minTimeLimit", Inv(t.minTimeLimit));
        Row("interRoundDelay", Inv(t.interRoundDelay));
        Row("resultFlashDuration", Inv(t.resultFlashDuration));
        Row("playerTurnDuration", Inv(t.playerTurnDuration));
        if (t.playerColors != null)
        {
            for (int i = 0; i < t.playerColors.Length; i++)
                sb.AppendLine($"{Quote("playerColor_" + (i+1))},{Quote(t.playerColors[i] ?? "")},{Quote((i+1) + "Pの色 (#RRGGBB)")}");
        }
        if (t.playerCallouts != null)
        {
            for (int i = 0; i < t.playerCallouts.Length; i++)
                sb.AppendLine($"{Quote("playerCallout_" + (i+1))},{Quote(t.playerCallouts[i] ?? "")},{Quote((i+1) + "Pに切り替わったときのセリフ")}");
        }
        if (t.customParams != null)
        {
            foreach (var p in t.customParams)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.key)) continue;
                sb.AppendLine($"{Quote(p.key)},{Quote(p.value ?? "")},{Quote(p.description ?? "")}");
            }
        }
        WriteUtf8Bom(Path.Combine(CsvDir, TuningCsv), sb.ToString());
    }

    // ------------ CSV utilities ------------
    private static List<List<string>> ReadCsv(string path)
    {
        string text = File.ReadAllText(path, Encoding.UTF8);
        if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
        var rows = new List<List<string>>();
        var cur = new List<string>();
        var cell = new StringBuilder();
        bool inQuote = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuote = false;
                }
                else cell.Append(c);
            }
            else
            {
                if (c == '"') inQuote = true;
                else if (c == ',') { cur.Add(cell.ToString()); cell.Length = 0; }
                else if (c == '\n' || c == '\r')
                {
                    if (cell.Length > 0 || cur.Count > 0) { cur.Add(cell.ToString()); cell.Length = 0; rows.Add(cur); cur = new List<string>(); }
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else cell.Append(c);
            }
        }
        if (cell.Length > 0 || cur.Count > 0) { cur.Add(cell.ToString()); rows.Add(cur); }
        return rows;
    }

    private static Dictionary<string, int> MakeIndex(List<string> header)
    {
        var d = new Dictionary<string, int>();
        for (int i = 0; i < header.Count; i++) d[header[i].Trim()] = i;
        return d;
    }

    private static string Get(List<string> row, Dictionary<string, int> col, string key)
    {
        if (!col.TryGetValue(key, out int i) || i >= row.Count) return "";
        return row[i];
    }

    private static int ParseInt(string s, int fallback)
    {
        if (int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
        return fallback;
    }
    private static float ParseFloat(string s, float fallback)
    {
        if (float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
        return fallback;
    }
    private static bool ParseBool(string s, bool fallback)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        if (s == "1" || s == "true" || s == "yes" || s == "y") return true;
        if (s == "0" || s == "false" || s == "no" || s == "n") return false;
        return fallback;
    }
    private static T ParseEnum<T>(string s, T fallback) where T : struct, Enum
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return fallback;
        if (Enum.TryParse<T>(s, true, out var v)) return v;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
        {
            try { return (T)Enum.ToObject(typeof(T), i); }
            catch { return fallback; }
        }
        return fallback;
    }
    private static string[] SplitList(string s, char sep = ';')
    {
        if (string.IsNullOrEmpty(s)) return new string[0];
        var parts = s.Split(sep);
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    private static string Quote(string s)
    {
        s = s ?? "";
        bool needs = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needs) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
    private static string Inv(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static string ColorToHex(Color c)
    {
        var c32 = (Color32)c;
        return $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}";
    }

    private static void WriteUtf8Bom(string path, string content)
    {
        var enc = new UTF8Encoding(true); // emit BOM for Excel compatibility
        File.WriteAllText(path, content, enc);
    }
}
#endif
