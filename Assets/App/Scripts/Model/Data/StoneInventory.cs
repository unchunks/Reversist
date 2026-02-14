using System;
using System.Collections.Generic;

[Serializable]
public class StoneInventory
{
    // 各石の所持数（-1 なら無限）
    public Dictionary<StoneType, int> Stock = new Dictionary<StoneType, int>();

    public StoneType LastSelected = StoneType.Normal;

    public StoneInventory()
    {
        // デフォルトの所持数設定 (バランス調整はここで行う)
        Stock[StoneType.Normal] = -1; // 無限
        Stock[StoneType.Expander] = 1;
        Stock[StoneType.Bomb] = 1;
        Stock[StoneType.Phantom] = 3;
        Stock[StoneType.Spy] = 1;
        Stock[StoneType.Fixed] = 2;
    }

    public bool CanUse(StoneType type)
    {
        if (!Stock.ContainsKey(type)) return false;
        return Stock[type] == -1 || Stock[type] > 0;
    }

    public void Use(StoneType type)
    {
        if (Stock.ContainsKey(type) && Stock[type] > 0)
        {
            Stock[type]--;
        }
    }

    public List<StoneType> GetAvailableStoneTypes()
    {
        List<StoneType> availableTypes = new List<StoneType>();
        foreach (var kvp in Stock)
        {
            if (CanUse(kvp.Key))
            {
                availableTypes.Add(kvp.Key);
            }
        }
        return availableTypes;
    }
}
