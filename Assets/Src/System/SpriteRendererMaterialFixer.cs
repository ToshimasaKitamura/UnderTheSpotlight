using UnityEngine;

/// <summary>
/// URP 2D環境でSprite Lit 2D Materialを使用しているスプライトが
/// Light2Dなしで表示されない問題を修正するコンポーネント。
///
/// 対処法1: Global Light 2Dを追加する（GlobalLight2DSetup）
/// 対処法2: マテリアルをSprites-Default（Unlit）に変更する（このスクリプト）
///
/// このスクリプトは対処法2を実行する。
/// </summary>
public class SpriteRendererMaterialFixer : MonoBehaviour
{
    [Tooltip("修正対象のSpriteRenderer。空の場合はシーン内のすべてを対象にする。")]
    public SpriteRenderer[] targetRenderers;

    [Tooltip("Sprites-Defaultマテリアル（Unlit）。未設定の場合は自動取得。")]
    public Material spritesDefaultMaterial;

    private void Awake()
    {
        FixMaterials();
    }

    [ContextMenu("Fix Materials Now")]
    public void FixMaterials()
    {
        // Sprites-Default (Unlit) マテリアルを取得
        if (spritesDefaultMaterial == null)
        {
            // Shader.Find で Unlit シェーダーを取得して新規マテリアル作成
            var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlitShader == null)
            {
                // フォールバック: 旧式のSprites-Defaultシェーダー
                unlitShader = Shader.Find("Sprites/Default");
            }

            if (unlitShader != null)
            {
                spritesDefaultMaterial = new Material(unlitShader);
                spritesDefaultMaterial.name = "Sprite-Unlit-Runtime";
                Debug.Log($"[SpriteRendererMaterialFixer] Unlitマテリアルを作成: shader={unlitShader.name}");
            }
            else
            {
                Debug.LogError("[SpriteRendererMaterialFixer] Unlitシェーダーが見つかりません！");
            }
        }

        if (spritesDefaultMaterial == null)
        {
            Debug.LogError("[SpriteRendererMaterialFixer] Sprites-Defaultマテリアルを取得できませんでした！");
            return;
        }

        // 対象のSpriteRendererを取得
        SpriteRenderer[] renderers = targetRenderers;
        if (renderers == null || renderers.Length == 0)
        {
            renderers = FindObjectsOfType<SpriteRenderer>();
        }

        int fixedCount = 0;
        foreach (var sr in renderers)
        {
            if (sr == null) continue;

            // 現在のマテリアルがSprite-Lit-Default（URP 2D Lit）かチェック
            var currentMat = sr.sharedMaterial;
            if (currentMat != null && currentMat.name.Contains("Lit"))
            {
                sr.sharedMaterial = spritesDefaultMaterial;
                fixedCount++;
                Debug.Log($"[SpriteRendererMaterialFixer] {sr.gameObject.name} のマテリアルをSprites-Defaultに変更しました");
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"[SpriteRendererMaterialFixer] 合計 {fixedCount} 個のSpriteRendererのマテリアルを修正しました");
        }
        else
        {
            Debug.Log("[SpriteRendererMaterialFixer] 修正が必要なSpriteRendererはありませんでした");
        }
    }

    /// <summary>
    /// デバッグ：シーン内のすべてのSpriteRendererのマテリアル情報をログ出力
    /// </summary>
    [ContextMenu("Debug: List All SpriteRenderer Materials")]
    public void DebugListMaterials()
    {
        var renderers = FindObjectsOfType<SpriteRenderer>();
        Debug.Log($"[SpriteRendererMaterialFixer] シーン内のSpriteRenderer: {renderers.Length}個");

        foreach (var sr in renderers)
        {
            var mat = sr.sharedMaterial;
            string matName = mat != null ? mat.name : "NULL";
            string spriteName = sr.sprite != null ? sr.sprite.name : "NULL";
            Debug.Log($"  {sr.gameObject.name}: material={matName}, sprite={spriteName}, enabled={sr.enabled}, color.a={sr.color.a}");
        }
    }
}
