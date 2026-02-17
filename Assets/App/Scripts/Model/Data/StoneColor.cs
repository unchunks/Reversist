public enum StoneColor : byte
{
    None = 0,
    Black = 1,
    White = 2,
    Wall = 255
}

public static class StoneColorExtensions
{
    public static StoneColor GetOpposite(this StoneColor color) => color == StoneColor.Black ? StoneColor.White : StoneColor.Black;
}