using System;
using System.Collections.Generic;

public class StoneCountManager
{    
    public Dictionary<State, StoneCount> StoneCounts { get; private set; }

    public StoneCountManager()
    {
        StoneCounts[State.Black] = new StoneCount();
        StoneCounts[State.White] = new StoneCount();
    }
}

public class StoneCount
{
    public event Action<Dictionary<StoneType, int>> OnCountChanged;
    private Dictionary<StoneType, int> _count;

    public StoneCount()
    {
        _count = new Dictionary<StoneType, int>
        {
            {StoneType.Normal, 64},
            {StoneType.Reverse, 5},
            {StoneType.DelayReverse, 5},
            {StoneType.Frozen, 1}
        };
        OnCountChanged?.Invoke(new Dictionary<StoneType, int>(_count));
    }

    /// <summary>
    /// 石を追加する
    /// </summary>
    public void AddCount(StoneType type, int count = 1)
    {
        if (!_count.ContainsKey(type))
        {
            _count[type] = 0;
        }

        _count[type] += count;
        OnCountChanged?.Invoke(new Dictionary<StoneType, int>(_count));
    }

    /// <summary>
    /// 石を削除する（0未満にはならない）
    /// </summary>
    public void RemoveCount(StoneType type, int count = 1)
    {
        if (_count.ContainsKey(type))
        {
            _count[type] = Math.Max(0, _count[type] - count);
            OnCountChanged?.Invoke(new Dictionary<StoneType, int>(_count));
        }
    }

    /// <summary>
    /// 現在の石の数を取得
    /// </summary>
    public int GetCount(StoneType type)
    {
        return _count.ContainsKey(type) ? _count[type] : 0;
    }
}