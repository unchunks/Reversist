namespace App.Reversi.Messaging
{
    /// <summary>
    /// ユーザーが盤面をクリックしたことを通知するメッセージ
    /// </summary>
    public class CellClickedMessage
    {
        public readonly Position Position;

        public CellClickedMessage(Position position)
        {
            Position = position;
        }
    }
}