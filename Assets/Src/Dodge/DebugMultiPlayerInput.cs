using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// デバッグ用: キーボード1台で複数プレイヤーのコントローラを擬似再現する入力実装。
/// 1〜4 キーを「押している間」、その番号のプレイヤーとしての入力を提供する。
/// turnSource が設定されている場合、押下番号が現在のアクティブプレイヤーと
/// 一致するときのみ入力を通す(=ターン外のプレイヤーが操作しても無効)。
/// </summary>
public class DebugMultiPlayerInput : MonoBehaviour, IPlayerOnFieldInput
{
    [Tooltip("操作権の真のソース。一致した番号を押している時のみ移動が通る。")]
    public PlayerTurnManager turnSource;

    public Vector2 ReadMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        int held = -1;
        if (kb.digit1Key.isPressed) held = 0;
        else if (kb.digit2Key.isPressed) held = 1;
        else if (kb.digit3Key.isPressed) held = 2;
        else if (kb.digit4Key.isPressed) held = 3;

        if (held < 0) return Vector2.zero;
        if (turnSource != null && held != turnSource.CurrentActivePlayer) return Vector2.zero;

        Vector2 v = Axis(kb.dKey, kb.aKey, kb.wKey, kb.sKey)
                  + Axis(kb.rightArrowKey, kb.leftArrowKey, kb.upArrowKey, kb.downArrowKey);
        v.x = Mathf.Clamp(v.x, -1f, 1f);
        v.y = Mathf.Clamp(v.y, -1f, 1f);
        return v;
    }

    private static Vector2 Axis(KeyControl right, KeyControl left, KeyControl up, KeyControl down)
    {
        float x = (right.isPressed ? 1f : 0f) - (left.isPressed ? 1f : 0f);
        float y = (up.isPressed ? 1f : 0f) - (down.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }
}
