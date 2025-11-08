namespace App.Reversi.Messaging
{
    /// <summary>
    /// ゲームが終了したことを通知するメッセージ
    /// </summary>
    public class GameOverMessage
    {
        public readonly StoneColor Winner; // 勝者の色、引き分けの場合はNone
        public readonly int BlackStonesCount; // 黒の石の数
        public readonly int WhiteStonesCount; // 白の石の数

        public GameOverMessage(StoneColor winner, int blackStonesCount, int whiteStonesCount)
        {
            Winner = winner;
            BlackStonesCount = blackStonesCount;
            WhiteStonesCount = whiteStonesCount;
        }
    }
}