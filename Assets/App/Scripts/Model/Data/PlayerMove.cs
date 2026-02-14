public struct PlayerMove
{
    public Position Pos;
    public StoneType Type;
    public StoneColor PlayerColor;
    public static PlayerMove Invalid => new PlayerMove { PlayerColor = StoneColor.None };
}
