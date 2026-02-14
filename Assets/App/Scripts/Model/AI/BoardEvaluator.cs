using UnityEngine;

// ---------------------------------------------------------
// AI: Board Evaluation Logic (Advanced)
// 着手可能数（Mobility）と確定石（Stability）を重視した評価関数
// ---------------------------------------------------------

public static class BoardEvaluator
{
    // 評価の重み係数（ゲームバランスの肝）
    private const int W_MOBILITY = 15;   // 着手可能数：相手の手を減らすのは強力
    private const int W_STABILITY = 30;  // 確定石：絶対に返されない石の価値
    private const int W_POSITION = 10;   // 場所：X打ちなどを避ける
    private const int W_COUNT = 2;       // 石数：序中盤はあまり重要ではない

    // 場所の基本価値
    private const int SCORE_CORNER = 100;
    private const int SCORE_X_SQUARE = -50; // 角の斜め手前（危険）
    private const int SCORE_C_SQUARE = -20; // 角の隣（やや危険）

    public static int Evaluate(BoardState board, StoneColor myColor)
    {
        StoneColor oppColor = (myColor == StoneColor.Black) ? StoneColor.White : StoneColor.Black;

        // 1. 盤面スキャン (石数、位置、特殊石)
        int myCount = 0, oppCount = 0;
        int myPosScore = 0, oppPosScore = 0;
        int myFixedBonus = 0, oppFixedBonus = 0;

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var cell = board.GetCell(x, y);
                if (cell.IsEmpty) continue;

                // 位置スコア
                int posVal = GetPositionWeight(x, y, board.Width, board.Height);

                if (cell.Color == myColor)
                {
                    myCount++;
                    myPosScore += posVal;
                    // Fixed石は確定石としてのボーナス
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

        // 2. 着手可能数 (Mobility)
        // 自分が打てる場所が多く、相手が打てる場所が少ないほど有利
        int myMobility = CountValidMoves(board, myColor);
        int oppMobility = CountValidMoves(board, oppColor);

        // 3. 辺の安定石 (Edge Stability)
        // BombやExtenderなどで不安定になる可能性があるため省略した方が良いかも
        // 角から繋がっている辺の石はひっくり返されない
        int myStableEdges = CountStableEdgeStones(board, myColor);
        int oppStableEdges = CountStableEdgeStones(board, oppColor);

        // --- 総合スコア計算 ---
        int score = 0;

        // Mobility差分
        score += (myMobility - oppMobility) * W_MOBILITY;

        // Stability (Fixed石 + 辺の安定石)
        // 角(Corner)はPosScoreに含まれているが、安定石としても加算する
        score += ((myFixedBonus + myStableEdges) - (oppFixedBonus + oppStableEdges)) * W_STABILITY;

        // 位置評価
        score += (myPosScore - oppPosScore) * W_POSITION;

        // 石数（盤面制圧力）
        score += (myCount - oppCount) * W_COUNT;

        return score;
    }

    // 有効手の数をカウント (重い処理なので注意。LogicAI側で計算済みなら渡すべきだが、独立性のためここで計算)
    private static int CountValidMoves(BoardState board, StoneColor color)
    {
        int count = 0;
        // 全マス走査はコストがかかるが、正確な評価には必須
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.GetCell(x, y).IsEmpty) continue;

                // 種類はNormalで仮定して配置可否のみチェック
                var move = new PlayerMove { Pos = new Position(x, y), PlayerColor = color, Type = StoneType.Normal };
                if (ReversiRules.IsValidMove(board, move))
                {
                    count++;
                }
            }
        }
        return count;
    }

    // 辺にある「確定石」をカウントする（角から連続している同色の石）
    private static int CountStableEdgeStones(BoardState board, StoneColor color)
    {
        int w = board.Width;
        int h = board.Height;
        int stableCount = 0;

        // 4つの辺をチェック
        // (dx, dy) = (1,0) 上辺, (0,1) 左辺...

        // 上辺 (Top)
        stableCount += CountLineStability(board, color, 0, 0, 1, 0, w);
        // 下辺 (Bottom)
        stableCount += CountLineStability(board, color, 0, h - 1, 1, 0, w);
        // 左辺 (Left)
        stableCount += CountLineStability(board, color, 0, 0, 0, 1, h);
        // 右辺 (Right)
        stableCount += CountLineStability(board, color, w - 1, 0, 0, 1, h);

        return stableCount;
    }

    // 一直線の辺上で、両端（角）から連続する自分の石を数える
    private static int CountLineStability(BoardState board, StoneColor color, int startX, int startY, int dx, int dy, int length)
    {
        int count = 0;

        // 始点（角1）からの連続
        for (int i = 0; i < length; i++)
        {
            var cell = board.GetCell(startX + dx * i, startY + dy * i);
            if (cell.Color == color) count++;
            else break; // 途切れたら終了
        }

        // 終点（角2）からの連続
        // 全て埋まっている場合（count == length）は重複カウントになるので除外したいが、
        // 簡易評価として「全部埋まってれば超強い」ので加算されても許容範囲とする。
        // ここでは重複を避けるため、未走査部分だけ逆からチェックする
        if (count < length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                var cell = board.GetCell(startX + dx * i, startY + dy * i);
                if (cell.Color == color) count++;
                else break;
            }
        }

        return count;
    }

    private static int GetPositionWeight(int x, int y, int width, int height)
    {
        bool isEdgeX = (x == 0 || x == width - 1);
        bool isEdgeY = (y == 0 || y == height - 1);
        bool isCorner = isEdgeX && isEdgeY;

        if (isCorner) return SCORE_CORNER;

        // X-Square / C-Square 判定
        bool isNextToCornerX = (x == 1 || x == width - 2);
        bool isNextToCornerY = (y == 1 || y == height - 2);

        // 角が空いている場合のX打ちは超危険
        // (厳密には盤面を見て角の空きを確認すべきだが、静的評価としてマイナスをつける)
        if (isNextToCornerX && isNextToCornerY) return SCORE_X_SQUARE;

        if ((isEdgeX && isNextToCornerY) || (isEdgeY && isNextToCornerX)) return SCORE_C_SQUARE;

        if (isEdgeX || isEdgeY) return 10; // 辺は悪くない

        return 0;
    }
}
