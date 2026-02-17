using System.Collections.Generic;

/// <summary>
/// StoneType‚©‚çStrategy‚ðŽæ“¾‚·‚éƒŒƒWƒXƒgƒŠ
/// </summary>
public static class StoneRegistry
{
    private static readonly StoneStrategy[] _strategies;

    static StoneRegistry()
    {
        _strategies = new StoneStrategy[(int)StoneType.Size];

        _strategies[(int)StoneType.Normal] = new NormalStoneStrategy();
        _strategies[(int)StoneType.Expander] = new ExpanderStoneStrategy();
        _strategies[(int)StoneType.Fixed] = new FixedStoneStrategy();
        _strategies[(int)StoneType.Phantom] = new PhantomStoneStrategy();
        _strategies[(int)StoneType.Bomb] = new BombStoneStrategy();
        _strategies[(int)StoneType.Spy] = new SpyStoneStrategy();
    }

    public static StoneStrategy Get(StoneType type)
    {
        return _strategies[(int)type];
    }
}
