using App.Reversi.Core;

namespace App.Reversi.Messaging
{
    /// <summary>
    /// ゲームが開始したことを通知するメッセージ
    /// </summary>
    public class GameStartMessage
    {
        public readonly GameMode GameMode;
        public readonly StoneColor AiColor;

        public GameStartMessage(GameMode GameMode, StoneColor AiColor)
        {
            this.GameMode = GameMode;
            this.AiColor = AiColor;
        }
    }
}