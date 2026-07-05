using UnityEngine;

public static class DodgePhaseUtils
{
    // ワールド変換結果が極端に大きな値かどうかを判断する（安全チェック）
    public static bool IsReasonablePosition(Vector3 v)
    {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return false;
        if (float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)) return false;
        // 大きすぎる値は非合理と判断（ワールド座標で +/-10000 より外なら怪しい）
        const float limit = 10000f;
        if (Mathf.Abs(v.x) > limit || Mathf.Abs(v.y) > limit || Mathf.Abs(v.z) > limit) return false;
        return true;
    }
}
