using System;
using System.Collections.Generic;

/// <summary>
/// 利用可能なミニゲームの登録簿。new で IMicroGame を生成するファクトリを保持し、
/// ランダム選出する。種類を増やすときは Register を1行足すだけ。
/// </summary>
public class MicroGameRegistry
{
    private readonly List<Func<IMicroGame>> factories = new List<Func<IMicroGame>>();
    private readonly List<string> names = new List<string>();

    public int Count => factories.Count;

    public void Register(Func<IMicroGame> factory)
    {
        if (factory == null) return;
        factories.Add(factory);
        names.Add(factory().DisplayName);
    }

    /// <summary>既定のミニゲーム群を登録(増やすときはここに足す)。</summary>
    public static MicroGameRegistry CreateDefault()
    {
        var r = new MicroGameRegistry();
        r.Register(() => new MashMicroGame());
        r.Register(() => new TimingMicroGame());
        r.Register(() => new SequenceMicroGame());
        r.Register(() => new HoldMicroGame());
        return r;
    }

    /// <summary>
    /// グループ型(全員で1回・リレー式)アクションコマンドの登録簿。ActionCommandPhase はここからランダムに選ぶ。
    /// 増やすときは GroupRelayMicroGame を継承したクラスを作り、ここに1行足す。
    /// 並び順は ActionCommandPhase.debugForceGroupGame の番号と対応する(0 始まり)。
    /// </summary>
    public static MicroGameRegistry CreateGroupDefault()
    {
        var r = new MicroGameRegistry();
        r.Register(() => new GroupMashMicroGame());      // 0: れんだ！
        r.Register(() => new GroupTimingMicroGame());    // 1: タイミング！
        r.Register(() => new GroupHoldMicroGame());      // 2: ためて はなせ！
        r.Register(() => new GroupSequenceMicroGame());  // 3: じゅんばんに！
        return r;
    }

    /// <summary>index 番目のミニゲームを生成する。範囲外はエラーを出して null。</summary>
    public IMicroGame Create(int index, out string displayName)
    {
        if (index < 0 || index >= factories.Count)
        {
            UnityEngine.Debug.LogError($"[MicroGameRegistry] index={index} は範囲外です(登録数 {factories.Count})。");
            displayName = "";
            return null;
        }
        var g = factories[index]();
        displayName = g.DisplayName;
        return g;
    }

    public IMicroGame CreateRandom(System.Random rng, out string displayName)
    {
        if (factories.Count == 0) { displayName = ""; return null; }
        int i = rng.Next(factories.Count);
        var g = factories[i]();
        displayName = g.DisplayName;
        return g;
    }
}
