namespace App.Reversi.Messaging
{
    /// <summary>
    /// ターンが変更されたことを通知するメッセージ
    /// </summary>
    public class TurnChangedMessage
    {
        public readonly StoneColor CurrentPlayer;

        public TurnChangedMessage(StoneColor currentPlayer)
        {
            CurrentPlayer = currentPlayer;
        }
    }
}