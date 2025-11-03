namespace App.Reversi.Messaging
{
    /// <summary>
    /// 石を置くことをリクエストするメッセージ
    /// </summary>
    public class RequestPutStoneMessage
    {
        public readonly StoneColor Player;
        public readonly StoneType Type;
        public readonly Position Position;

        public RequestPutStoneMessage(StoneColor player, StoneType type, Position position)
        {
            Player = player;
            Type = type;
            Position = position;
        }
    }
}