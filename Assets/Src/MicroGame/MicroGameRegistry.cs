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

    public IMicroGame CreateRandom(System.Random rng, out string displayName)
    {
        if (factories.Count == 0) { displayName = ""; return null; }
        int i = rng.Next(factories.Count);
        var g = factories[i]();
        displayName = g.DisplayName;
        return g;
    }
}
