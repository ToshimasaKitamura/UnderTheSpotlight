using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// デッキ構成定義(ScriptableObject)。
/// 「どのカードを何枚入れるか」をデータで持つ。30枚固定でなく自由に組める。
/// 仕様例(基本20/特殊9/必殺1=30枚)はこのアセット上で設定する(コードにハードコードしない)。
/// ステージ進行イベントで内容を差し替える場合は、別アセットを用意するか
/// DeckController の AddCard/RemoveCard を使う。
/// </summary>
[CreateAssetMenu(fileName = "DeckDefinition", menuName = "UnderTheSpotlight/DeckDefinition")]
public class DeckDefinition : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public CardData card;
        [Min(1)] public int copies;
    }

    [Tooltip("デッキに含めるカードと枚数。")]
    public List<Entry> entries = new List<Entry>();

    /// <summary>総枚数(全 copies の合計)。</summary>
    public int TotalCards
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].card != null) sum += Mathf.Max(0, entries[i].copies);
            }
            return sum;
        }
    }
}
