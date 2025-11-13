using App.Reversi;
using App.Reversi.AI;
using App.Reversi.Core;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine; // For Debug.Log

namespace App.Reversi.AI
{
    public static class BoardStateConverter
    {
        /// <summary>
        /// UnityのBoardからOptimizedGameStateを効率的に構築する
        /// </summary>
        public static GameState GetCurrentGameState(
            this Board board,
            StoneColor currentPlayer,
            Dictionary<StoneColor, AvailableStoneCount> inventories)
        {
            var state = GameState.GetFromPool();

            state.CurrentPlayer = currentPlayer;
            state.CurrentBoardSize = board.CurrentBoardSize;
            state.IsGameOver = false;
            state.ValidActionsCache = null;

            CopyInventory(inventories[StoneColor.Black], state.BlackInventory);
            CopyInventory(inventories[StoneColor.White], state.WhiteInventory);

            state.DelayReverseStack.Clear();
            state.DelayReverseStack.AddRange(board.DelayReverseStack);

            Array.Clear(state.BlackStones, 0, GameState.BITBOARD_UINT64_COUNT);
            Array.Clear(state.WhiteStones, 0, GameState.BITBOARD_UINT64_COUNT);
            Array.Clear(state.StoneTypeBits0, 0, GameState.BITBOARD_UINT64_COUNT);
            Array.Clear(state.StoneTypeBits1, 0, GameState.BITBOARD_UINT64_COUNT);
            Array.Clear(state.StoneTypeBits2, 0, GameState.BITBOARD_UINT64_COUNT);

            state.BlackCount = 0;
            state.WhiteCount = 0;

            Cell[,] boardCells = board.BoardCells;
            for (int r = 0; r < GameState.MAX_BOARD_SIZE; r++)
            {
                for (int c = 0; c < GameState.MAX_BOARD_SIZE; c++)
                {
                    Cell cell = boardCells[r, c];
                    if (cell != null && cell.isPlased)
                    {
                        int index = r * GameState.MAX_BOARD_SIZE + c;
                        int arrayIndex = index / 64;
                        int bitIndex = index % 64;
                        ulong mask = 1UL << bitIndex;

                        //Debug.Log($"[BoardStateConverter] Stone at ({r},{c}). Index: {index}, ArrayIdx: {arrayIndex}, BitIdx: {bitIndex}, Mask: {mask:X16}, Color: {cell.Color}");

                        if (cell.Color == StoneColor.Black)
                        {
                            state.BlackStones[arrayIndex] |= mask;
                            state.BlackCount++;
                        }
                        else if (cell.Color == StoneColor.White)
                        {
                            state.WhiteStones[arrayIndex] |= mask;
                            state.WhiteCount++;
                        }

                        int typeInt = (int)cell.Type;
                        if ((typeInt & 1) != 0) state.StoneTypeBits0[arrayIndex] |= mask;
                        if ((typeInt & 2) != 0) state.StoneTypeBits1[arrayIndex] |= mask;
                        if ((typeInt & 4) != 0) state.StoneTypeBits2[arrayIndex] |= mask;
                    }
                }
            }

            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyInventory(AvailableStoneCount source, int[] destination)
        {
            destination[0] = source.AvailableCount[StoneType.Normal];
            destination[1] = source.AvailableCount[StoneType.Extend];
            destination[2] = source.AvailableCount[StoneType.Frozen];
            destination[3] = source.AvailableCount[StoneType.Reverse];
            destination[4] = source.AvailableCount[StoneType.DelayReverse];
        }
    }
}