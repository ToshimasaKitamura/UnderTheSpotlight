using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// キーボードからの自機入力。playerIndex に応じてキー割当てを切り替える。
/// IPlayerOnFieldInput を実装し、PlayerOnFieldController から参照される。
/// </summary>
public class KeyboardPlayerOnFieldInput : MonoBehaviour, IPlayerOnFieldInput
{
    [Tooltip("0: WASD+矢印 / 1: 矢印のみ / 2: IJKL / 3: Numpad8456")]
    public int playerIndex = 0;

    public Vector2 ReadMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        Vector2 v;
        switch (playerIndex)
        {
            case 0:
                // WASD と 矢印 のどちらでも操作可能
                v = Axis(kb.dKey, kb.aKey, kb.wKey, kb.sKey)
                  + Axis(kb.rightArrowKey, kb.leftArrowKey, kb.upArrowKey, kb.downArrowKey);
                break;
            case 1: v = Axis(kb.rightArrowKey, kb.leftArrowKey, kb.upArrowKey, kb.downArrowKey); break;
            case 2: v = Axis(kb.lKey, kb.jKey, kb.iKey, kb.kKey); break;
            case 3: v = Axis(kb.numpad6Key, kb.numpad4Key, kb.numpad8Key, kb.numpad2Key); break;
            default: return Vector2.zero;
        }
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
