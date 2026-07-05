# スプライトが表示されない問題のトラブルシューティング

## 症状
- Scene viewではスプライトが見えるが、Game viewでは見えない
- SpriteRendererのsprite、sortingOrder、enabled、color.aはすべて正常
- エラーログは出ていない

## 原因と解決策

### 1. カメラのZ位置が0になっている

**症状**: 2DゲームでカメラのZ位置が0だと、Z=0にあるスプライトがカメラと同じ位置になり表示されない。

**確認方法**:
```csharp
Debug.Log($"カメラ位置: {Camera.main.transform.position}");
// Z=0 または Z>0 だと問題
```

**解決策**: カメラのZ位置を-10に設定する。
```csharp
var cam = Camera.main;
cam.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, -10f);
```

**本プロジェクトでの原因**:
`BossIntroPhase.Exit()`が、`Enter()`が呼ばれる前に実行されると、未初期化の`originalCameraPosition`（Vector3.zero）にカメラをリセットしてしまっていた。

**修正**: `hasEntered`フラグを追加し、`Enter()`が呼ばれた場合のみカメラ位置をリセットするようにした。

---

### 2. URP 2D環境でSprite-Lit-Defaultマテリアルを使用しているがLight2Dがない

**症状**: URP 2D Rendererを使用し、SpriteRendererにSprite-Lit-Defaultマテリアルが設定されている場合、シーンにLight2Dがないとスプライトが完全に暗くなる（見えなくなる）。

**確認方法**:
```csharp
var sr = GetComponent<SpriteRenderer>();
Debug.Log($"マテリアル: {sr.sharedMaterial.name}");
// "Sprite-Lit-Default" や "Lit" を含む名前だと問題の可能性
```

**解決策A**: シーンにGlobal Light 2Dを追加する。
```csharp
using UnityEngine.Rendering.Universal;

var lightObj = new GameObject("Global Light 2D");
var light2d = lightObj.AddComponent<Light2D>();
light2d.lightType = Light2D.LightType.Global;
light2d.intensity = 1f;
```

**解決策B**: マテリアルをUnlitに変更する。
```csharp
var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
if (unlitShader != null)
{
    var unlitMaterial = new Material(unlitShader);
    spriteRenderer.sharedMaterial = unlitMaterial;
}
```

---

### 3. 新規SpriteRendererのデフォルトマテリアルがLitになる

**症状**: URP 2D環境では、新しくSpriteRendererを作成すると自動的にSprite-Lit-Defaultマテリアルが割り当てられる。

**原因**: `Assets/Settings/Renderer2D.asset`の`m_DefaultMaterialType: 0`（Lit）設定。

**確認方法**:
```csharp
var tempGo = new GameObject("Temp");
var tempSr = tempGo.AddComponent<SpriteRenderer>();
Debug.Log($"デフォルトマテリアル: {tempSr.sharedMaterial.name}");
// "Sprite-Lit-Default" と表示される
```

---

## デバッグ用コンポーネント

本プロジェクトには以下のデバッグ用コンポーネントが含まれている：

- `CameraDebugger.cs` - カメラ状態の確認とZ位置の自動修正
- `SpriteRendererMaterialFixer.cs` - 全SpriteRendererのマテリアルをUnlitに変更
- `GlobalLight2DSetup.cs` - Global Light 2Dの自動生成

これらはBattleStateMachineのAwakeで自動生成される。

---

## チェックリスト

スプライトが表示されない場合、以下を順番に確認：

1. [ ] SpriteRenderer.sprite が null でないか
2. [ ] SpriteRenderer.enabled が true か
3. [ ] SpriteRenderer.color.a が 0 でないか
4. [ ] GameObject.activeInHierarchy が true か
5. [ ] カメラのcullingMaskにレイヤーが含まれているか
6. [ ] カメラのZ位置が負の値か（2Dの場合、通常-10）
7. [ ] スプライトがカメラの表示範囲内にあるか
8. [ ] URP 2D環境の場合、Light2Dがあるか、またはUnlitマテリアルを使用しているか
