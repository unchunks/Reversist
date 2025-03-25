namespace App.Reversi
{
    public enum StoneColor
    {
        Black,
        White,
        None,
    }

    public static class StoneColorExtentions
    {
        public static StoneColor Opponent(this StoneColor color)
        {
            switch (color)
            {
                case StoneColor.Black:  return StoneColor.White;
                case StoneColor.White:  return StoneColor.Black;
                default:                return StoneColor.None;
            }
        }

        public static string ToString(this StoneColor color)
        {
            switch (color)
            {
                case StoneColor.Black:  return "黒";
                case StoneColor.White:  return "白";
                default:                return "なし";
            }
        }
    }
}