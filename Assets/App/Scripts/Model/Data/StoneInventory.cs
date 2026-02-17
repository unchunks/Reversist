using System;
using System.Collections.Generic;

[Serializable]
public class StoneInventory
{
    // 各石の所持数（-1 なら無限）
    public int[] Stock = new int[(int)StoneType.Size];

    public StoneType LastSelected = StoneType.Normal;

    public StoneInventory()
    {
        // デフォルトの所持数設定
        Stock[(int)StoneType.Normal] = -1; // 無限
        Stock[(int)StoneType.Expander] = 1;
        Stock[(int)StoneType.Bomb] = 1;
        Stock[(int)StoneType.Phantom] = 3;
        Stock[(int)StoneType.Spy] = 1;
        Stock[(int)StoneType.Fixed] = 2;
    }

    public void CopyTo(StoneInventory dst)
    {
        Array.Copy(this.Stock, dst.Stock, this.Stock.Length);
        dst.LastSelected = this.LastSelected;
    }

    public bool CanUse(StoneType type)
    {
        int count = Stock[(int)type];
        return count == -1 || count > 0;
    }

    public void Use(StoneType type)
    {
        int index = (int)type;
        if (Stock[index] > 0)
        {
            Stock[index]--;
        }
    }

    // AI用：アロケーションを避けるため、既存のバッファ(Listや配列)に詰め込むメソッドを追加
    public int GetAvailableStoneTypesNonAlloc(Span<StoneType> buffer)
    {
        int count = 0;
        for (int i = 0; i < (int)StoneType.Size; i++)
        {
            if (Stock[i] == -1 || Stock[i] > 0)
            {
                buffer[count++] = (StoneType)i;
            }
        }
        return count; // 有効な種類数を返す
    }
}
