namespace App.Reversi.AI
{
    /// <summary>
    /// AIが実行可能な1つの行動（手）を定義する
    /// </summary>
    public class GameAction
    {
        public readonly Position Position;
        public readonly StoneType Type;
        public readonly StoneColor Player;

        public GameAction(Position position, StoneType type, StoneColor player)
        {
            Position = position;
            Type = type;
            Player = player;
        }
    }
}