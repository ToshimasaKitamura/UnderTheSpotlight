using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Data/csv/*.csv が変更・保存されたら、自動で CSV を反映する。
/// Excel で編集して保存 → Unity にフォーカスを戻すだけでカード等に反映される
/// (手動で「Reimport from CSV」メニューを実行する必要がなくなる)。
///
/// 反映は <see cref="CsvImporter.ReimportAllSilent"/>(ダイアログ無し)を使う。
/// 生成される .asset の再インポートで無限ループしないよう、対象は .csv のみ + 実行中ガード + delayCall。
/// </summary>
public class CsvAutoImporter : AssetPostprocessor
{
    private const string CsvDirPrefix = "Assets/Data/csv/";
    private static bool busy;

    private static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (busy) return;

        bool csvChanged = false;
        foreach (var p in importedAssets)
        {
            string n = p.Replace('\\', '/');
            if (n.StartsWith(CsvDirPrefix) && n.EndsWith(".csv")) { csvChanged = true; break; }
        }
        if (!csvChanged) return;

        // インポートコールバック中に AssetDatabase を触ると不安定なので、次のフレームで実行。
        EditorApplication.delayCall += () =>
        {
            if (busy) return;
            busy = true;
            try
            {
                CsvImporter.ReimportAllSilent();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CsvAutoImporter] CSV 自動インポートに失敗: " + e);
            }
            finally
            {
                busy = false;
            }
        };
    }
}
