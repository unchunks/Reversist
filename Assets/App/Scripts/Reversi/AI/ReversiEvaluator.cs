using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Reversi.AI
{
    /// <summary>
    /// 評価関数（位置テーブル + 確定石 + Mobility + 危機回避）
    /// </summary>
    public static class ReversiEvaluator
    {
        // 8x8の基本評価テーブル
        private static readonly int[,] EvalTable8x8 = {
            { 150, -30,  20,   5,   5,  20, -30, 150 },
            { -30, -60,  -5,  -5,  -5,  -5, -60, -30 },
            {  20,  -5,  15,   3,   3,  15,  -5,  20 },
            {   5,  -5,   3,   3,   3,   3,  -5,   5 },
            {   5,  -5,   3,   3,   3,   3,  -5,   5 },
            {  20,  -5,  15,   3,   3,  15,  -5,  20 },
            { -30, -60,  -5,  -5,  -5,  -5, -60, -30 },
            { 150, -30,  20,   5,   5,  20, -30, 150 }
        };

        private static Dictionary<int, int[,]> _tableCache = new Dictionary<int, int[,]>();

        static ReversiEvaluator()
        {
            _tableCache[8] = EvalTable8x8;
            _tableCache[10] = GenerateTable(10);
            _tableCache[12] = GenerateTable(12);
        }

        private static int[,] GenerateTable(int size)
        {
            int[,] table = new int[size, size];
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    int val = 1;
                    bool isCornerR = (r == 0 || r == size - 1);
                    bool isCornerC = (c == 0 || c == size - 1);

                    if (isCornerR && isCornerC) val = 150;
                    else if ((r <= 1 || r >= size - 2) && (c <= 1 || c >= size - 2))
                    {
                        if (isCornerR || isCornerC) val = -30;
                        else val = -60;
                    }
                    else if (isCornerR || isCornerC) val = 20;
                    else val = 5;

                    table[r, c] = val;
                }
            }
            return table;
        }

        public static double Evaluate(GameState state)
        {
            // 終局時の評価
            if (state.IsGameOver)
            {
                int diff = state.StoneCount[StoneColor.Black] - state.StoneCount[StoneColor.White];
                if (diff > 0) return 20000.0 + diff;
                if (diff < 0) return -20000.0 + diff;
                return 0.0;
            }

            // 全滅リスク評価（Survival Instinct）
            // 石数が少ない状態は、すぐ負ける
            int blackCount = state.StoneCount[StoneColor.Black];
            int whiteCount = state.StoneCount[StoneColor.White];

            if (blackCount <= 2) return -18000.0; // 全滅の一歩手前
            if (whiteCount <= 2) return 18000.0;

            int size = state.CurrentBoardSize;
            if (!_tableCache.TryGetValue(size, out int[,] table))
            {
                table = GenerateTable(size);
                _tableCache[size] = table;
            }

            double blackScore = 0;
            double whiteScore = 0;
            int offset = (GameState.MAX_BOARD_SIZE - size) / 2;

            // 位置評価
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    int boardR = r + offset;
                    int boardC = c + offset;

                    StoneColor color = state.Board[boardR, boardC];
                    if (color != StoneColor.None)
                    {
                        int val = table[r, c];
                        if (color == StoneColor.Black) blackScore += val;
                        else whiteScore += val;
                    }
                }
            }

            // Mobility (着手可能数)
            int validMoves = ReversiSimulator.GetValidActions(state).Count;
            double mobilityBonus = validMoves * 10.0;

            if (state.CurrentPlayer == StoneColor.Black) blackScore += mobilityBonus;
            else whiteScore += mobilityBonus;

            // DelayReverseスタックの評価（負債リスク）
            // DelayReverseは「敵に回る可能性が高い裏切り者」として扱う。
            foreach (var delayItem in state.DelayReverseStack)
            {
                Position pos = delayItem.Pos;
                if (pos.Row >= 0 && pos.Row < GameState.MAX_BOARD_SIZE &&
                    pos.Col >= 0 && pos.Col < GameState.MAX_BOARD_SIZE)
                {
                    StoneColor color = state.Board[pos.Row, pos.Col];

                    // 爆心地の重要度
                    int r = pos.Row - offset;
                    int c = pos.Col - offset;
                    int posValue = 10;
                    if (r >= 0 && r < size && c >= 0 && c < size)
                    {
                        posValue = Math.Max(10, Math.Abs(table[r, c]));
                    }

                    // 懲罰的ペナルティ
                    // Delay石を持っている側（裏切られる側）に大きなマイナス
                    double penalty = 200.0 + (posValue * 10.0);

                    if (color == StoneColor.Black) blackScore -= penalty;
                    else if (color == StoneColor.White) whiteScore -= penalty;
                }
            }

            return blackScore - whiteScore;
        }
    }
}
