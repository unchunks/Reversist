public static class BoardEvaluator
{
    private const int W_MOBILITY = 15;
    private const int W_STABILITY = 30;
    private const int W_POSITION = 10;
    private const int W_COUNT = 2;

    // 12x12の静的評価テーブル
    private static readonly int[] POSITION_WEIGHTS = new int[BoardState.MAX_SIZE * BoardState.MAX_SIZE]
    {
        120, -20,  20,   5,   5,   5,   5,   5,   5,  20, -20, 120, // Y = 0
        -20, -40,  -5,  -5,  -5,  -5,  -5,  -5,  -5,  -5, -40, -20, // Y = 1
         20,  -5,  15,   3,   3,   3,   3,   3,   3,  15,  -5,  20, // Y = 2
          5,  -5,   3,   3,   3,   3,   3,   3,   3,   3,  -5,   5, // Y = 3
          5,  -5,   3,   3,   3,   3,   3,   3,   3,   3,  -5,   5, // Y = 4
          5,  -5,   3,   3,   3,   3,   3,   3,   3,   3,  -5,   5, // Y = 5
          5,  -5,   3,   3,   3,   3,   3,   3,   3,   3,  -5,   5, // Y = 6
          5,  -5,   3,   3,   3,   3,   3,   3,   3,   3,  -5,   5, // Y = 7
          5,  -5,   3,   3,   3,   3,   3,   3,   3,   3,  -5,   5, // Y = 8
         20,  -5,  15,   3,   3,   3,   3,   3,   3,  15,  -5,  20, // Y = 9
        -20, -40,  -5,  -5,  -5,  -5,  -5,  -5,  -5,  -5, -40, -20, // Y = 10
        120, -20,  20,   5,   5,   5,   5,   5,   5,  20, -20, 120  // Y = 11
    };

    public static int EvaluateWithMove(BoardState board, StoneColor myColor, PlayerMove move)
    {
        if (move.Type == StoneType.Expander && board.Width < BoardState.MAX_SIZE && board.Height < BoardState.MAX_SIZE)
        {
            // 自分だけが拡張石を使っている場合、角をとると逆に不利になるため、選ばないようにする
            return int.MinValue + 1;
        }

        return Evaluate(board, myColor);
    }

    public static int Evaluate(BoardState board, StoneColor myColor)
    {
        StoneColor oppColor = myColor.GetOpposite();

        int myCount = 0, oppCount = 0;
        int myPosScore = 0, oppPosScore = 0;
        int myFixedBonus = 0, oppFixedBonus = 0;

        for (int y = 0; y < board.Height; y++)
        {
            // Y軸の絶対座標を計算
            int realY = board.OriginY + y;
            int rowOffset = realY * BoardState.MAX_SIZE;

            for (int x = 0; x < board.Width; x++)
            {
                var cell = board.GetCell(x, y);
                if (cell.IsEmpty) continue;

                int realX = board.OriginX + x;
                int posVal = POSITION_WEIGHTS[rowOffset + realX];

                if (cell.Color == myColor)
                {
                    myCount++;
                    myPosScore += posVal;
                    if (cell.Type == StoneType.Fixed || cell.IsFixed) myFixedBonus++;
                }
                else if (cell.Color == oppColor)
                {
                    oppCount++;
                    oppPosScore += posVal;
                    if (cell.Type == StoneType.Fixed || cell.IsFixed) oppFixedBonus++;
                }
            }
        }

        int myMobility = CountValidMovesFast(board, myColor);
        int oppMobility = CountValidMovesFast(board, oppColor);

        // 真の確定石のみをカウント
        int myStableEdges = CountTrueStableEdgeStones(board, myColor);
        int oppStableEdges = CountTrueStableEdgeStones(board, oppColor);

        int score = 0;
        score += (myMobility - oppMobility) * W_MOBILITY;
        score += ((myFixedBonus + myStableEdges) - (oppFixedBonus + oppStableEdges)) * W_STABILITY;
        score += (myPosScore - oppPosScore) * W_POSITION;
        score += (myCount - oppCount) * W_COUNT;

        return score;
    }

    private static int CountValidMovesFast(BoardState board, StoneColor color)
    {
        int count = 0;
        PlayerMove testMove = new PlayerMove { PlayerColor = color, Type = StoneType.Normal };

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.GetCell(x, y).IsEmpty) continue;
                testMove.Pos.x = x;
                testMove.Pos.y = y;

                if (ReversiRules.IsValidMove(board, testMove)) count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 「絶対に拡張されない真の辺」に到達している場合のみ確定石として評価する
    /// </summary>
    private static int CountTrueStableEdgeStones(BoardState board, StoneColor color)
    {
        int stableCount = 0;

        // 真の上辺 (物理配列のY=0)
        if (board.OriginY == 0)
            stableCount += CountLineStability(board, color, 0, 0, 1, 0, board.Width);

        // 真の下辺 (物理配列のY=11)
        if (board.OriginY + board.Height == BoardState.MAX_SIZE)
            stableCount += CountLineStability(board, color, 0, board.Height - 1, 1, 0, board.Width);

        // 真の左辺 (物理配列のX=0)
        if (board.OriginX == 0)
            stableCount += CountLineStability(board, color, 0, 0, 0, 1, board.Height);

        // 真の右辺 (物理配列のX=11)
        if (board.OriginX + board.Width == BoardState.MAX_SIZE)
            stableCount += CountLineStability(board, color, board.Width - 1, 0, 0, 1, board.Height);

        return stableCount;
    }

    private static int CountLineStability(BoardState board, StoneColor color, int startX, int startY, int dx, int dy, int length)
    {
        int count = 0;

        // スタート地点から順方向へ連続している石をカウント
        for (int i = 0; i < length; i++)
        {
            if (board.GetCell(startX + dx * i, startY + dy * i).Color == color) count++;
            else break;
        }

        // 全て一色でなければ、逆方向からも確認
        if (count < length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                if (board.GetCell(startX + dx * i, startY + dy * i).Color == color) count++;
                else break;
            }
        }
        return count;
    }
}
