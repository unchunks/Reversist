[System.Serializable]
public struct CellData
{
    public StoneColor Color;
    public StoneType Type;
    public bool IsFixed;
    public bool IsEmpty => Color == StoneColor.None;
}
