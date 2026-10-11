using System.Collections.Generic;

/// <summary>
/// バトルに参加するパーティ(キャラクター選択の結果)の単一ソース。
///
/// キャラ選択では <see cref="GameSceneManager.selectedParty"/>[4] に
/// 参加者=CharacterData / 非参加=null が入る。バトル各フェーズはこれを直接見ず
/// 固定4人(<c>tuning.playerCount</c>)で動いていたため、ここで「参加者だけを前詰め」した
/// 配列と実効人数を提供し、各フェーズの playerCount をこれに合わせる。
///
/// - 参加者が1人以上いる → 前詰め配列(長さ=参加人数)。四隅の枠は先頭から n 個が埋まり、残りは空枠。
/// - 参加者が0(= キャラ選択を経ていない直接再生/デバッグ) → null を返し、呼び出し側は従来の tuning/シーン既定へフォールバック。
/// </summary>
public static class BattleParty
{
    /// <summary>参加者の CharacterData を前詰めした配列。参加者0なら null。</summary>
    public static CharacterData[] Resolve()
    {
        var gsm = GameSceneManager.Instance;
        if (gsm == null || gsm.selectedParty == null) return null;

        var list = new List<CharacterData>();
        foreach (var c in gsm.selectedParty)
            if (c != null) list.Add(c);

        return list.Count > 0 ? list.ToArray() : null;
    }

    /// <summary>
    /// バトルの実効プレイヤー人数。参加者がいればその人数、いなければ tuning の既定値(無ければ4)。
    /// </summary>
    public static int Count(GameTuning tuning)
    {
        var party = Resolve();
        if (party != null) return party.Length;
        return tuning != null ? tuning.playerCount : 4;
    }
}
