using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1人分のデッキ(山札/手札/捨札)を管理するプレーン C# クラス。
/// MonoBehaviour ではない。PartyDeckController がプレイヤー人数分を保持する。
///
/// 仕様:
///   - DeckDefinition から山札を構築・シャッフル
///   - DrawHand(n) で n 枚引く(直前の手札は捨札へ)
///   - 山札が尽きたら捨札を戻し、強化レベル +1 して全カードに再付与し再シャッフル
///   - ステージイベント用 AddCard / RemoveCard / RebuildFrom
/// </summary>
public sealed class PlayerDeck
{
    public int UpgradeLevel { get; private set; }
    public int DrawPileCount => drawPile.Count;
    public int DiscardPileCount => discardPile.Count;
    public int TotalCards => drawPile.Count + discardPile.Count + hand.Count;
    public IReadOnlyList<CardInstance> Hand => hand;

    /// <summary>山札枯渇→再シャッフル(強化Lv上昇)時に発火。引数=新Lv。</summary>
    public event Action<int> DeckCycled;

    private readonly List<CardInstance> drawPile = new List<CardInstance>();
    private readonly List<CardInstance> discardPile = new List<CardInstance>();
    private readonly List<CardInstance> hand = new List<CardInstance>();
    private readonly System.Random rng;

    public PlayerDeck(DeckDefinition definition, int seed)
    {
        rng = new System.Random(seed);
        RebuildFrom(definition);
    }

    public void RebuildFrom(DeckDefinition definition)
    {
        drawPile.Clear();
        discardPile.Clear();
        hand.Clear();
        UpgradeLevel = 0;

        if (definition != null)
        {
            foreach (var e in definition.entries)
            {
                if (e.card == null) continue;
                int copies = Mathf.Max(0, e.copies);
                for (int i = 0; i < copies; i++)
                    drawPile.Add(new CardInstance(e.card, UpgradeLevel));
            }
        }
        Shuffle(drawPile);
    }

    public IReadOnlyList<CardInstance> DrawHand(int count)
    {
        DiscardHand();
        int n = Mathf.Max(0, count);
        for (int i = 0; i < n; i++)
        {
            if (drawPile.Count == 0)
            {
                CycleDeck();
                if (drawPile.Count == 0) break;
            }
            int last = drawPile.Count - 1;
            hand.Add(drawPile[last]);
            drawPile.RemoveAt(last);
        }
        return hand;
    }

    public void DiscardHand()
    {
        if (hand.Count == 0) return;
        discardPile.AddRange(hand);
        hand.Clear();
    }

    public void AddCard(CardData card, int copies = 1)
    {
        if (card == null) return;
        for (int i = 0; i < Mathf.Max(1, copies); i++)
        {
            int insert = rng.Next(drawPile.Count + 1);
            drawPile.Insert(insert, new CardInstance(card, UpgradeLevel));
        }
    }

    public void RemoveCard(CardData card, int copies = 1)
    {
        if (card == null) return;
        int remaining = Mathf.Max(1, copies);
        remaining -= RemoveFrom(drawPile, card, remaining);
        if (remaining > 0) RemoveFrom(discardPile, card, remaining);
    }

    private static int RemoveFrom(List<CardInstance> pile, CardData card, int max)
    {
        int removed = 0;
        for (int i = pile.Count - 1; i >= 0 && removed < max; i--)
        {
            if (pile[i].Data == card)
            {
                pile.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    private void CycleDeck()
    {
        UpgradeLevel++;
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        for (int i = 0; i < drawPile.Count; i++)
            drawPile[i].SetUpgradeLevel(UpgradeLevel);
        Shuffle(drawPile);
        DeckCycled?.Invoke(UpgradeLevel);
    }

    private void Shuffle(List<CardInstance> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
