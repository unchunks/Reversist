using App.Reversi.Core;
using System.Collections.Generic;

namespace App.Reversi
{
    public class ReverseCountDown
    {
        public Position Pos { get; private set; }
        public int Delay { get; set; }

        public ReverseCountDown(Position pos, int delay)
        {
            Pos = pos;
            Delay = delay;
        }

        /// <summary>
        /// ディープコピー用コンストラクタ
        /// </summary>
        public ReverseCountDown(ReverseCountDown source)
        {
            Pos = new Position(source.Pos);
            Delay = source.Delay;
        }
    }
}