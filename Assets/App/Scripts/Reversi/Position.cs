namespace App.Reversi
{
    /// <summary>
    /// 盤面の位置を表すクラス
    /// </summary>
    public class Position
    {
        public int Row { get; }
        public int Col { get; }

        public Position(int row, int col)
        {
            Row = row;
            Col = col;
        }

        /// <summary>
        /// ディープコピー用コンストラクタ
        /// </summary>
        public Position(Position source)
        {
            Row = source.Row;
            Col = source.Col;
        }

        public override bool Equals(object obj)
        {
            if (obj is Position other)
            {
                return Row == other.Row && Col == other.Col;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Board.MAX_BOARD_SIZE * Row + Col;
        }
    }

}