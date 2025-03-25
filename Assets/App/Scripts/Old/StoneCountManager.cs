using System;
using System.Collections.Generic;

public class StoneCountManager
{    
    public Dictionary<State, AvailableStoneCount> StoneCounts { get; private set; }

    public StoneCountManager()
    {
        StoneCounts[State.Black] = new AvailableStoneCount();
        StoneCounts[State.White] = new AvailableStoneCount();
    }
}

public class AvailableStoneCount
{
    public ObservableDictionary<StoneType, int> Count { get; private set; }

    public AvailableStoneCount()
    {
        Count = new ObservableDictionary<StoneType, int>();
        ResetCount();
    }

    public void ResetCount()
    {
        Count[StoneType.Normal] = 53;
        Count[StoneType.Reverse] = 5;
        Count[StoneType.DelayReverse] = 5;
        Count[StoneType.Frozen] = 1;
    }

    /// <summary>
    /// 石を追加する
    /// </summary>
    public void AddCount(StoneType type, int count = 1)
    {
        if (!Count.ContainsKey(type))
        {
            Count[type] = 0;
        }

        Count[type] += count;
    }

    /// <summary>
    /// 石を削除する（0未満にはならない）
    /// </summary>
    public void RemoveCount(StoneType type, int count = 1)
    {
        if (Count.ContainsKey(type))
        {
            Count[type] = Math.Max(0, Count[type] - count);
        }
    }

    /// <summary>
    /// 現在の石の数を取得
    /// </summary>
    public int GetCount(StoneType type)
    {
        return Count.ContainsKey(type) ? Count[type] : 0;
    }
}