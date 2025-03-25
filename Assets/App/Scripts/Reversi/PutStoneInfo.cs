namespace App.Reversi
{
    public class PutStoneInfo
    {
        public StoneColor Color;
        public StoneType Type;
        
        public PutStoneInfo(StoneColor color, StoneType type)
        {
            Color = color;
            Type = type;
        }
    }
}