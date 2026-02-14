using System.Collections.Generic;

/// <summary>
/// StoneTypeからStrategyを取得するレジストリ
/// switch文をここだけに限定し、各所での分岐をなくす
/// </summary>
public static class StoneRegistry
{
    private static readonly Dictionary<StoneType, StoneStrategy> _strategies;

    static StoneRegistry()
    {
        _strategies = new Dictionary<StoneType, StoneStrategy>
        {
            { StoneType.Normal, new NormalStoneStrategy() },
            { StoneType.Expander, new ExpanderStoneStrategy() },
            { StoneType.Fixed, new FixedStoneStrategy() },
            { StoneType.Phantom, new PhantomStoneStrategy() },
            { StoneType.Bomb, new BombStoneStrategy() },
            { StoneType.Spy, new SpyStoneStrategy() },
        };
    }

    public static StoneStrategy Get(StoneType type)
    {
        // NOTE: 配列にしてもいいかも
        if (_strategies.TryGetValue(type, out var strategy))
        {
            return strategy;
        }
        return _strategies[StoneType.Normal];
    }
}
