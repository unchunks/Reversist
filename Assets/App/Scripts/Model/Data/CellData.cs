[System.Serializable]
public struct CellData
{
    public StoneColor Color;
    public StoneType Type;
    public bool IsFixed => Type == StoneType.Fixed;
    public bool IsEmpty => Color == StoneColor.None;
}
