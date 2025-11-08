namespace App.Reversi.Messaging
{
    /// <summary>
    /// AIが思考中であることをUIに通知するメッセージ
    /// </summary>
    public class AIThinkingMessage
    {
        public readonly bool IsThinking;
        public readonly StoneColor AiColor;

        public AIThinkingMessage(StoneColor aiColor, bool isThinking)
        {
            AiColor = aiColor;
            IsThinking = isThinking;
        }
    }
}
