using System;
using System.Collections.Generic;

namespace App.Reversi.AI
{
    // ============================================================================
    // 改良版評価関数
    // ============================================================================
    public static class ReversiEvaluator
    {
        private static readonly Position[] Dirs = {
            new Position(-1, -1), new Position(-1, 0), new Position(-1, 1),
            new Position(0, -1), new Position(0, 1),
            new Position(1, -1), new Position(1, 0), new Position(1, 1)
        };

        /// <summary>
        /// メイン評価関数
        /// </summary>
        public static double Evaluate(GameState state)
        {
            if (state.IsGameOver)
            {
                return EvaluateTerminal(state);
            }

            double progress = CalculateProgress(state);

            // 5つの評価軸
            double cornerScore = EvaluateCorners(state);           // コーナー制御
            double stabilityScore = EvaluateStability(state);      // 安定性
            double mobilityScore = EvaluateMobility(state);        // 機動力
            double potentialScore = EvaluatePotential(state);      // ポテンシャル
            double specialStoneScore = EvaluateSpecialStones(state, progress); // 特殊石

            // 進行度に応じた重み付け
            double cornerWeight = 0.4 + (progress * 0.3);      // 0.4 -> 0.7
            double stabilityWeight = 0.2 + (progress * 0.2);   // 0.2 -> 0.4
            double mobilityWeight = 0.3 - (progress * 0.2);    // 0.3 -> 0.1
            double potentialWeight = 0.1 - (progress * 0.05);  // 0.1 -> 0.05
            double specialWeight = 0.15;                        // 常に0.15

            return (cornerScore * cornerWeight) +
                   (stabilityScore * stabilityWeight) +
                   (mobilityScore * mobilityWeight) +
                   (potentialScore * potentialWeight) +
                   (specialStoneScore * specialWeight);
        }

        private static double EvaluateTerminal(GameState state)
        {
            int diff = state.StoneCount[StoneColor.Black] - state.StoneCount[StoneColor.White];
            if (diff > 0) return 100000.0;
            if (diff < 0) return -100000.0;
            return 0.0;
        }

        private static double CalculateProgress(GameState state)
        {
            int total = state.StoneCount[StoneColor.Black] + state.StoneCount[StoneColor.White];
            int max = state.CurrentBoardSize * state.CurrentBoardSize;
            return Math.Pow((double)total / max, 1.5);
        }

        /// <summary>
        /// コーナー評価（最重要）
        /// </summary>
        private static double EvaluateCorners(GameState state)
        {
            double score = 0.0;
            int size = state.CurrentBoardSize;
            Position[] corners = Board.GetCorners(size);

            foreach (var corner in corners)
            {
                StoneColor stone = state.Board[corner.Row, corner.Col];
                if (stone == StoneColor.Black)
                {
                    score += 800.0;
                }
                else if (stone == StoneColor.White)
                {
                    score -= 800.0;
                }
                else
                {
                    // コーナー周辺の危険地帯ペナルティ
                    score += EvaluateCornerDangerZone(state, corner);
                }
            }

            return score;
        }

        private static double EvaluateCornerDangerZone(GameState state, Position corner)
        {
            double penalty = 0.0;
            int size = state.CurrentBoardSize;

            // X位置（対角）
            int xRow = corner.Row + (corner.Row == 0 ? 1 : -1);
            int xCol = corner.Col + (corner.Col == 0 ? 1 : -1);
            if (ReversiSimulator.IsInBoard(xRow, xCol, size))
            {
                if (state.Board[xRow, xCol] == StoneColor.Black) penalty -= 200.0;
                if (state.Board[xRow, xCol] == StoneColor.White) penalty += 200.0;
            }

            // C位置（縦横の隣）
            int[] cRows = { corner.Row, corner.Row + (corner.Row == 0 ? 1 : -1) };
            int[] cCols = { corner.Col + (corner.Col == 0 ? 1 : -1), corner.Col };

            foreach (int cRow in cRows)
            {
                foreach (int cCol in cCols)
                {
                    if (cRow == corner.Row && cCol == corner.Col) continue;
                    if (ReversiSimulator.IsInBoard(cRow, cCol, size))
                    {
                        if (state.Board[cRow, cCol] == StoneColor.Black) penalty -= 100.0;
                        if (state.Board[cRow, cCol] == StoneColor.White) penalty += 100.0;
                    }
                }
            }

            return penalty;
        }

        /// <summary>
        /// 安定性評価
        /// </summary>
        private static double EvaluateStability(GameState state)
        {
            double score = 0.0;
            int size = state.CurrentBoardSize;

            // 辺の評価
            for (int i = 0; i < size; i++)
            {
                // 上辺
                score += EvaluateEdgeStone(state, 0, i);
                // 下辺
                score += EvaluateEdgeStone(state, size - 1, i);
                // 左辺
                score += EvaluateEdgeStone(state, i, 0);
                // 右辺
                score += EvaluateEdgeStone(state, i, size - 1);
            }

            return score;
        }

        private static double EvaluateEdgeStone(GameState state, int row, int col)
        {
            StoneColor stone = state.Board[row, col];
            if (stone == StoneColor.None) return 0.0;

            double value = 30.0;
            if (stone == StoneColor.Black) return value;
            if (stone == StoneColor.White) return -value;
            return 0.0;
        }

        /// <summary>
        /// 機動力評価
        /// </summary>
        private static double EvaluateMobility(GameState state)
        {
            int blackMoves = CountMoves(state, StoneColor.Black);
            int whiteMoves = CountMoves(state, StoneColor.White);

            int totalMoves = blackMoves + whiteMoves;
            if (totalMoves == 0) return 0.0;

            double mobilityRatio = ((double)(blackMoves - whiteMoves) / totalMoves) * 100.0;

            // 着手不可能は致命的
            if (state.CurrentPlayer == StoneColor.Black && blackMoves == 0) mobilityRatio -= 300.0;
            if (state.CurrentPlayer == StoneColor.White && whiteMoves == 0) mobilityRatio += 300.0;

            return mobilityRatio;
        }

        private static int CountMoves(GameState state, StoneColor color)
        {
            GameState tempState = new GameState(state);
            tempState.CurrentPlayer = color;
            return ReversiSimulator.GetValidActions(tempState).Count;
        }

        /// <summary>
        /// ポテンシャル評価
        /// </summary>
        private static double EvaluatePotential(GameState state)
        {
            double progress = CalculateProgress(state);
            int stoneDiff = state.StoneCount[StoneColor.Black] - state.StoneCount[StoneColor.White];

            // 序盤は石が少ない方が有利、終盤は多い方が有利
            double stoneValue = stoneDiff * (-30.0 + (progress * 100.0));

            return stoneValue;
        }

        /// <summary>
        /// 特殊石評価（改良版）
        /// </summary>
        private static double EvaluateSpecialStones(GameState state, double progress)
        {
            double score = 0.0;

            // Extend（盤面拡大）
            int extendDiff = state.Inventories[StoneColor.Black].AvailableCount[StoneType.Extend] -
                           state.Inventories[StoneColor.White].AvailableCount[StoneType.Extend];
            double extendValue = 150.0 * Math.Pow(progress, 2);
            score += extendDiff * extendValue;

            // Reverse（即座反転）
            int reverseDiff = state.Inventories[StoneColor.Black].AvailableCount[StoneType.Reverse] -
                            state.Inventories[StoneColor.White].AvailableCount[StoneType.Reverse];
            double reverseValue = 100.0 * Math.Pow(progress, 1.5);
            score += reverseDiff * reverseValue;

            // DelayReverse（遅延反転）- 慎重な評価
            int delayDiff = state.Inventories[StoneColor.Black].AvailableCount[StoneType.DelayReverse] -
                          state.Inventories[StoneColor.White].AvailableCount[StoneType.DelayReverse];

            double delayValue;
            if (progress < 0.2)
            {
                delayValue = 10.0; // 序盤は危険
            }
            else if (progress < 0.6)
            {
                delayValue = 10.0 + (50.0 * (progress - 0.2) / 0.4); // 中盤で上昇
            }
            else
            {
                delayValue = 60.0 * Math.Pow(1.0 - progress, 2); // 終盤で急降下
            }

            // スタックに既にある場合は価値半減
            if (state.DelayReverseStack.Count > 0)
            {
                delayValue *= 0.5;
            }

            score += delayDiff * delayValue;

            // Frozen（凍結）
            int frozenDiff = state.Inventories[StoneColor.Black].AvailableCount[StoneType.Frozen] -
                           state.Inventories[StoneColor.White].AvailableCount[StoneType.Frozen];
            double frozenValue = 80.0 * Math.Pow(1.0 - progress, 1.5);
            score += frozenDiff * frozenValue;

            return score;
        }
    }
}
