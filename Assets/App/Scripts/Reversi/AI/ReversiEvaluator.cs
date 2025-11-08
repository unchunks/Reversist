using System;
using System.Collections.Generic;

namespace App.Reversi.AI
{
    /// <summary>
    /// GameStateを評価し、点数化する静的クラス
    /// </summary>
    public static class ReversiEvaluator
    {
        // 8方向の定義
        private static readonly Position[] Dirs = {
            new Position(-1, -1), new Position(-1, 0), new Position(-1, 1),
            new Position(0, -1), new Position(0, 1),
            new Position(1, -1), new Position(1, 0), new Position(1, 1)
        };

        /// <summary>
        /// 評価関数のメインエントリーポイント
        /// </summary>
        public static double Evaluate(GameState state)
        {
            // ゲーム終了時は絶対評価
            if (state.IsGameOver)
            {
                float result = ReversiSimulator.GetResult(state);
                if (result == 1.0f) return 1000000.0;
                if (result == -1.0f) return -1000000.0;
                return 0.0;
            }

            // ゲーム進行度 (0.0=序盤, 1.0=終盤)
            double progress = CalculateGameProgress(state);

            // 3つの評価軸
            double stability = EvaluateStability(state);      // 確実性: 確定石と安定性
            double mobility = EvaluateMobility(state);        // 柔軟性: 着手可能数と戦略的自由度
            double potential = EvaluatePotential(state);      // 潜在力: 特殊石と将来性

            // ゲーム進行に応じた動的ウェイト
            // 序盤: 柔軟性と潜在力を重視 (mobility 50%, potential 30%, stability 20%)
            // 終盤: 確実性を最重視 (stability 70%, mobility 20%, potential 10%)
            double stabilityWeight = 0.2 + (progress * 0.5);  // 0.2 -> 0.7
            double mobilityWeight = 0.5 - (progress * 0.3);   // 0.5 -> 0.2
            double potentialWeight = 0.3 - (progress * 0.2);  // 0.3 -> 0.1

            return (stability * stabilityWeight) +
                   (mobility * mobilityWeight) +
                   (potential * potentialWeight);
        }

        /// <summary>
        /// ゲーム進行度を計算 (0.0=序盤, 1.0=終盤)
        /// </summary>
        private static double CalculateGameProgress(GameState state)
        {
            int totalStones = state.StoneCount[StoneColor.Black] + state.StoneCount[StoneColor.White];
            int maxStones = state.CurrentBoardSize * state.CurrentBoardSize;

            // 序盤は緩やかに、終盤は急激に進行度を上げる (累乗で調整)
            double rawProgress = (double)totalStones / maxStones;
            return Math.Pow(rawProgress, 1.5);
        }

        /// <summary>
        /// 確実性の評価: 確定石とコーナー制御
        /// </summary>
        private static double EvaluateStability(GameState state)
        {
            double score = 0.0;
            int size = state.CurrentBoardSize;

            // コーナーの絶対的価値 (各コーナーを個別に評価)
            Position[] corners = Board.GetCorners(size);
            foreach (var corner in corners)
            {
                if (state.Board[corner.Row, corner.Col] == StoneColor.Black)
                    score += 500.0 + EvaluateCornerInfluence(state, corner, StoneColor.Black);
                else if (state.Board[corner.Row, corner.Col] == StoneColor.White)
                    score -= 500.0 + EvaluateCornerInfluence(state, corner, StoneColor.White);
            }

            // 辺の安定性 (コーナーに接続された辺は価値が高い)
            score += EvaluateEdgeStability(state);

            // 危険地帯ペナルティ (コーナー隣接マスのX, C位置)
            score += EvaluateDangerZones(state);

            return score;
        }

        /// <summary>
        /// コーナーからの影響範囲を評価
        /// </summary>
        private static double EvaluateCornerInfluence(GameState state, Position corner, StoneColor color)
        {
            double influence = 0.0;
            int size = state.CurrentBoardSize;

            // コーナーから連結している安定石の数を数える
            HashSet<Position> visited = new HashSet<Position>();
            Queue<Position> queue = new Queue<Position>();
            queue.Enqueue(corner);
            visited.Add(corner);

            while (queue.Count > 0)
            {
                Position current = queue.Dequeue();

                // 4方向（縦横のみ）で連結をチェック
                foreach (var dir in new[] { Dirs[1], Dirs[3], Dirs[4], Dirs[6] })
                {
                    Position next = new Position(current.Row + dir.Row, current.Col + dir.Col);

                    if (ReversiSimulator.IsInBoard(next.Row, next.Col, size) &&
                        !visited.Contains(next) &&
                        state.Board[next.Row, next.Col] == color)
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                        influence += 50.0; // 連結した安定石1つにつき+50
                    }
                }
            }

            return influence;
        }

        /// <summary>
        /// 辺の安定性を評価
        /// </summary>
        private static double EvaluateEdgeStability(GameState state)
        {
            double score = 0.0;
            int size = state.CurrentBoardSize;

            // 4辺をそれぞれチェック
            // 上辺
            score += EvaluateEdgeLine(state, 0, 0, 0, 1, size);
            // 下辺
            score += EvaluateEdgeLine(state, size - 1, 0, 0, 1, size);
            // 左辺
            score += EvaluateEdgeLine(state, 0, 0, 1, 0, size);
            // 右辺
            score += EvaluateEdgeLine(state, 0, size - 1, 1, 0, size);

            return score;
        }

        /// <summary>
        /// 一つの辺を評価
        /// </summary>
        private static double EvaluateEdgeLine(GameState state, int startRow, int startCol, int dRow, int dCol, int size)
        {
            double score = 0.0;
            int blackRun = 0, whiteRun = 0;

            for (int i = 0; i < size; i++)
            {
                int r = startRow + (dRow * i);
                int c = startCol + (dCol * i);

                if (state.Board[r, c] == StoneColor.Black)
                {
                    blackRun++;
                    if (whiteRun > 0)
                    {
                        // 連続が途切れた
                        whiteRun = 0;
                    }
                }
                else if (state.Board[r, c] == StoneColor.White)
                {
                    whiteRun++;
                    if (blackRun > 0)
                    {
                        blackRun = 0;
                    }
                }
                else
                {
                    // 空きマスで途切れる
                    blackRun = 0;
                    whiteRun = 0;
                }
            }

            // 連続した辺の石は価値が高い (2乗で評価)
            if (blackRun > 0) score += blackRun * blackRun * 10.0;
            if (whiteRun > 0) score -= whiteRun * whiteRun * 10.0;

            return score;
        }

        /// <summary>
        /// 危険地帯 (X, C位置) のペナルティ
        /// </summary>
        private static double EvaluateDangerZones(GameState state)
        {
            double score = 0.0;
            int size = state.CurrentBoardSize;
            Position[] corners = Board.GetCorners(size);

            foreach (var corner in corners)
            {
                // コーナーが空いている場合のみペナルティ
                if (state.Board[corner.Row, corner.Col] == StoneColor.None)
                {
                    // X位置 (対角)
                    int xRow = corner.Row + (corner.Row == 0 ? 1 : -1);
                    int xCol = corner.Col + (corner.Col == 0 ? 1 : -1);
                    if (ReversiSimulator.IsInBoard(xRow, xCol, size))
                    {
                        if (state.Board[xRow, xCol] == StoneColor.Black) score -= 100.0;
                        if (state.Board[xRow, xCol] == StoneColor.White) score += 100.0;
                    }

                    // C位置 (縦横の隣)
                    int[] cRows = { corner.Row, corner.Row + (corner.Row == 0 ? 1 : -1) };
                    int[] cCols = { corner.Col + (corner.Col == 0 ? 1 : -1), corner.Col };

                    foreach (int cRow in cRows)
                    {
                        foreach (int cCol in cCols)
                        {
                            if (cRow == corner.Row && cCol == corner.Col) continue;
                            if (ReversiSimulator.IsInBoard(cRow, cCol, size))
                            {
                                if (state.Board[cRow, cCol] == StoneColor.Black) score -= 50.0;
                                if (state.Board[cRow, cCol] == StoneColor.White) score += 50.0;
                            }
                        }
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// 柔軟性の評価: 着手可能数と戦略的自由度
        /// </summary>
        private static double EvaluateMobility(GameState state)
        {
            // 実際の着手可能数
            int blackMoves = CountMoves(state, StoneColor.Black);
            int whiteMoves = CountMoves(state, StoneColor.White);

            double mobilityScore = 0.0;
            int totalMoves = blackMoves + whiteMoves;

            if (totalMoves > 0)
            {
                // 相対的な着手可能数の差 (-100 ~ +100)
                mobilityScore = ((double)(blackMoves - whiteMoves) / totalMoves) * 100.0;
            }

            // 着手不可能は致命的 (パスを強制される)
            if (state.CurrentPlayer == StoneColor.Black && blackMoves == 0) mobilityScore -= 200.0;
            if (state.CurrentPlayer == StoneColor.White && whiteMoves == 0) mobilityScore += 200.0;

            // 潜在的着手可能性 (空きマスの周囲に相手の石があるか)
            mobilityScore += EvaluatePotentialMobility(state);

            return mobilityScore;
        }

        /// <summary>
        /// 潜在的な着手可能性を評価
        /// </summary>
        private static double EvaluatePotentialMobility(GameState state)
        {
            int blackPotential = 0;
            int whitePotential = 0;
            int size = state.CurrentBoardSize;

            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (state.Board[r, c] == StoneColor.None)
                    {
                        // 周囲に相手の石があれば、将来の着手候補
                        bool hasBlackNeighbor = false;
                        bool hasWhiteNeighbor = false;

                        foreach (var dir in Dirs)
                        {
                            int nr = r + dir.Row;
                            int nc = c + dir.Col;
                            if (ReversiSimulator.IsInBoard(nr, nc, size))
                            {
                                if (state.Board[nr, nc] == StoneColor.Black) hasBlackNeighbor = true;
                                if (state.Board[nr, nc] == StoneColor.White) hasWhiteNeighbor = true;
                            }
                        }

                        if (hasWhiteNeighbor) blackPotential++;
                        if (hasBlackNeighbor) whitePotential++;
                    }
                }
            }

            return (blackPotential - whitePotential) * 5.0;
        }

        /// <summary>
        /// 潜在力の評価: 特殊石と将来性
        /// </summary>
        private static double EvaluatePotential(GameState state)
        {
            double score = 0.0;

            // 特殊石の戦略的価値
            score += EvaluateSpecialStones(state);

            // 石数の差 (序盤は少ない方が有利、終盤は多い方が有利)
            double progress = CalculateGameProgress(state);
            int stoneDiff = state.StoneCount[StoneColor.Black] - state.StoneCount[StoneColor.White];

            // 序盤は石数が少ない方が有利 (柔軟性が高い)
            // 終盤は石数が多い方が有利 (勝利条件)
            double stoneValue = stoneDiff * (-50.0 + (progress * 150.0)); // 序盤-50, 終盤+100
            score += stoneValue;

            return score;
        }

        /// <summary>
        /// 特殊石の戦略的価値
        /// </summary>
        private static double EvaluateSpecialStones(GameState state)
        {
            double score = 0.0;
            double progress = CalculateGameProgress(state);

            // Extend: 盤面拡大の権利 (中盤以降に価値が急上昇)
            int extendDiff = state.Inventories[StoneColor.Black].AvailableCount[StoneType.Extend] -
                           state.Inventories[StoneColor.White].AvailableCount[StoneType.Extend];
            double extendValue = 100.0 * Math.Pow(progress, 2); // 0 -> 100 (2次関数的に増加)
            score += extendDiff * extendValue;

            // Reverse: 形勢逆転の可能性 (常に一定の価値)
            int reverseDiff = state.Inventories[StoneColor.Black].AvailableCount[StoneType.Reverse] -
                            state.Inventories[StoneColor.White].AvailableCount[StoneType.Reverse];
            score += reverseDiff * 80.0;

            // DelayReverse: 遅延戦術 (中盤で最も価値が高い)
            int delayDiff = state.Inventories[StoneColor.Black].AvailableCount[StoneType.DelayReverse] -
                          state.Inventories[StoneColor.White].AvailableCount[StoneType.DelayReverse];
            double delayValue = 60.0 * Math.Sin(progress * Math.PI); // 0 -> 60 -> 0 (中盤でピーク)
            score += delayDiff * delayValue;

            // Frozen: 挟み込みを阻害する障害物
            // 重要な位置（辺、コーナー付近）で相手の挟み込みルートを遮断すると価値が高い
            for (int r = 0; r < state.CurrentBoardSize; r++)
            {
                for (int c = 0; c < state.CurrentBoardSize; c++)
                {
                    if (state.StoneTypes[r, c] == StoneType.Frozen)
                    {
                        StoneColor frozenColor = state.Board[r, c];
                        double frozenValue = EvaluateFrozenPosition(state, new Position(r, c), frozenColor);
                        score += frozenValue;
                    }
                }
            }

            return score;
        }

        #region Helper Methods

        private static bool IsNearCorner(int row, int col, int size)
        {
            return (row <= 1 || row >= size - 2) && (col <= 1 || col >= size - 2);
        }

        private static int CountMoves(GameState state, StoneColor color)
        {
            GameState tempState = new GameState(state);
            tempState.CurrentPlayer = color;
            return ReversiSimulator.GetValidActions(tempState).Count;
        }

        /// <summary>
        /// Frozen石の戦略的価値を評価
        /// Frozenは挟み込みを阻害する障害物として機能
        /// </summary>
        private static double EvaluateFrozenPosition(GameState state, Position pos, StoneColor frozenColor)
        {
            double value = 0.0;
            int size = state.CurrentBoardSize;
            StoneColor opponent = frozenColor.Opponent();

            // 8方向それぞれで、Frozenが相手の挟み込みを阻害しているかチェック
            foreach (var dir in Dirs)
            {
                // この方向に相手の石があるか
                Position next = new Position(pos.Row + dir.Row, pos.Col + dir.Col);
                if (!ReversiSimulator.IsInBoard(next.Row, next.Col, size)) continue;
                if (state.Board[next.Row, next.Col] != opponent) continue;

                // 相手の石の連続を数える
                int opponentCount = 0;
                Position current = next;
                while (ReversiSimulator.IsInBoard(current.Row, current.Col, size) &&
                       state.Board[current.Row, current.Col] == opponent)
                {
                    opponentCount++;
                    current = new Position(current.Row + dir.Row, current.Col + dir.Col);
                }

                // Frozenの後ろに自分の石があるか（挟み込まれる危険）
                Position behind = new Position(pos.Row - dir.Row, pos.Col - dir.Col);
                bool hasOwnStonesBehind = false;
                if (ReversiSimulator.IsInBoard(behind.Row, behind.Col, size))
                {
                    Position check = behind;
                    while (ReversiSimulator.IsInBoard(check.Row, check.Col, size))
                    {
                        if (state.Board[check.Row, check.Col] == frozenColor)
                        {
                            hasOwnStonesBehind = true;
                            break;
                        }
                        if (state.Board[check.Row, check.Col] == opponent) break;
                        check = new Position(check.Row - dir.Row, check.Col - dir.Col);
                    }
                }

                // Frozenが相手の挟み込みルートを遮断している
                if (opponentCount > 0 && hasOwnStonesBehind)
                {
                    // 相手の連続石が多いほど、遮断の価値が高い
                    value += opponentCount * 15.0;
                }
            }

            // 戦略的に重要な位置にあるFrozenはさらに高評価
            // 1. 辺にある場合（辺は挟み込みの重要ルート）
            if (pos.Row == 0 || pos.Row == size - 1 || pos.Col == 0 || pos.Col == size - 1)
            {
                value += 30.0;
            }

            // 2. コーナー近くにある場合（ただしX位置は除く）
            if (IsNearCorner(pos.Row, pos.Col, size) && !IsXPosition(pos, size))
            {
                value += 25.0;
            }

            // 3. 中央付近にある場合（挟み込みルートが多い）
            int center = size / 2;
            int distFromCenter = Math.Abs(pos.Row - center) + Math.Abs(pos.Col - center);
            if (distFromCenter <= 2)
            {
                value += 20.0;
            }

            // 黒視点でのスコアに変換
            return frozenColor == StoneColor.Black ? value : -value;
        }

        /// <summary>
        /// X位置（コーナーの対角）かどうか
        /// </summary>
        private static bool IsXPosition(Position pos, int size)
        {
            return (pos.Row == 1 && pos.Col == 1) ||
                   (pos.Row == 1 && pos.Col == size - 2) ||
                   (pos.Row == size - 2 && pos.Col == 1) ||
                   (pos.Row == size - 2 && pos.Col == size - 2);
        }

        #endregion
    }
}