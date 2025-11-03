namespace App.Reversi.Messaging
{
    /// <summary>
    /// 石の在庫数が変更されたことを通知するメッセージ
    /// </summary>
    public class AvailableCountChangedMessage
    {
        public readonly StoneColor Color;
        public readonly StoneType Type;
        public readonly int Count;

        public AvailableCountChangedMessage(StoneColor color, StoneType type, int count)
        {
            Color = color;
            Type = type;
            Count = count;
        }
    }
}