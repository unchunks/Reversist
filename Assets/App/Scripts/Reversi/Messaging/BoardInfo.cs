using System.Collections.Generic;

namespace App.Reversi
{
    /// <summary>
    /// 盤上に石を置いたことを伝えるデータクラス
    /// </summary>
    public class BoardInfo
    {
        public StoneColor PutPlayer { get; private set; }
        public StoneColor PutColor { get; private set; }
        public StoneType PutType { get; private set; }
        public List<Position> ReversePos { get; private set; }
        public Dictionary<StoneColor, int> TotalStoneCount { get; private set; }

        public BoardInfo(StoneColor putPlayer, StoneColor putColor, StoneType putType, List<Position> reversePos, Dictionary<StoneColor, int> totalStoneCount)
        {
            PutPlayer = putPlayer;
            PutColor = putColor;
            PutType = putType;
            ReversePos = reversePos;
            TotalStoneCount = totalStoneCount;
        }
    }
}