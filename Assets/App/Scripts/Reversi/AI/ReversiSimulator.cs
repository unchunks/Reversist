using System;
using System.Collections.Generic;

namespace App.Reversi.AI
{
    /// <summary>
    /// Unityに依存せず、純粋なC#ロジックだけでゲームを高速にシミュレートする
    /// </summary>
    public static class ReversiSimulator
    {
        private static readonly Position[] Dirs = {
            new Position(-1, -1), new Position(-1, 0), new Position(-1, 1),
            new Position(0, -1), new Position(0, 1),
            new Position(1, -1), new Position(1, 0), new Position(1, 1)
        };

        // Positionオブジェクトの事前キャッシュ
        private static readonly Position[,] _positionCache;

        static ReversiSimulator()
        {
            _positionCache = new Position[GameState.MAX_BOARD_SIZE, GameState.MAX_BOARD_SIZE];
            for (int r = 0; r < GameState.MAX_BOARD_SIZE; r++)
            {
                for (int c = 0; c < GameState.MAX_BOARD_SIZE; c++)
                {
                    _positionCache[r, c] = new Position(r, c);
                }
            }
        }

        public static float GetResult(GameState state)
        {
            if (state.IsGameOver)
            {
                if (state.StoneCount[StoneColor.Black] > state.StoneCount[StoneColor.White]) return 1.0f;
                if (state.StoneCount[StoneColor.White] > state.StoneCount[StoneColor.Black]) return -1.0f;
                return 0.5f;
            }
            return 0.0f;
        }

        public static List<GameAction> GetValidActions(GameState state)
        {
            var actions = new List<GameAction>(32);
            StoneColor currentPlayer = state.CurrentPlayer;

            int size = state.CurrentBoardSize;
            int center = GameState.MAX_BOARD_SIZE / 2;
            int halfSize = size / 2;
            int minRow = center - halfSize;
            int maxRow = center + halfSize;

            foreach (var kvp in state.Inventories[currentPlayer].AvailableCount)
            {
                if (kvp.Value <= 0) continue;

                StoneType type = kvp.Key;
                bool isReverseType = type.IsReverseType();

                for (int r = minRow; r < maxRow; r++)
                {
                    for (int c = minRow; c < maxRow; c++)
                    {
                        if (state.Board[r, c] != StoneColor.None) continue;

                        Position pos = _positionCache[r, c];

                        if (CanReverse(state, currentPlayer, pos, size))
                        {
                            actions.Add(new GameAction(pos, type, currentPlayer));
                        }
                        else if (isReverseType && CanReverse(state, currentPlayer.Opponent(), pos, size))
                        {
                            actions.Add(new GameAction(pos, type, currentPlayer));
                        }
                    }
                }
            }
            return actions;
        }

        public static GameState ExecuteAction(GameState currentState, GameAction action)
        {
            GameState nextState = new GameState(currentState);

            nextState.Inventories[action.Player].Decrease(action.Type);

            StoneColor putColor = action.Type.IsReverseType() ? action.Player.Opponent() : action.Player;

            var reversePos = FindReversePos(nextState, putColor, action.Position);

            int r = action.Position.Row;
            int c = action.Position.Col;
            nextState.Board[r, c] = putColor;
            nextState.StoneTypes[r, c] = action.Type;
            nextState.StoneCount[putColor]++;

            int count = reversePos.Count;
            for (int i = 0; i < count; i++)
            {
                Position pos = reversePos[i];
                StoneColor originalColor = nextState.Board[pos.Row, pos.Col];
                if (nextState.StoneTypes[pos.Row, pos.Col] != StoneType.Frozen)
                {
                    nextState.Board[pos.Row, pos.Col] = originalColor.Opponent();
                    nextState.StoneCount[originalColor]--;
                    nextState.StoneCount[originalColor.Opponent()]++;
                }
            }

            ProcessSpecialStones(nextState, action);

            // 【修正】全滅（Wipeout）判定を追加
            // どちらかの石が0になったら、即座にゲーム終了とする
            if (nextState.StoneCount[StoneColor.Black] == 0 || nextState.StoneCount[StoneColor.White] == 0)
            {
                nextState.IsGameOver = true;
                return nextState;
            }

            nextState.CurrentPlayer = nextState.CurrentPlayer.Opponent();

            if (!HasValidMove(nextState))
            {
                nextState.CurrentPlayer = nextState.CurrentPlayer.Opponent();
                if (!HasValidMove(nextState))
                {
                    nextState.IsGameOver = true;
                }
            }

            return nextState;
        }

        private static void ProcessSpecialStones(GameState state, GameAction action)
        {
            for (int i = state.DelayReverseStack.Count - 1; i >= 0; i--)
            {
                var item = state.DelayReverseStack[i];
                item.Delay--;
                if (item.Delay <= 0)
                {
                    ExecuteDelayReverse(state, item.Pos);
                    state.DelayReverseStack.RemoveAt(i);
                }
                else
                {
                    state.DelayReverseStack[i] = item;
                }
            }

            switch (action.Type)
            {
                case StoneType.Extend:
                    state.CurrentBoardSize = Math.Min(state.CurrentBoardSize + 2, GameState.MAX_BOARD_SIZE);
                    break;
                case StoneType.Reverse:
                    ExecuteReverseEffect(state, action.Position);
                    break;
                case StoneType.DelayReverse:
                    state.DelayReverseStack.Add(new ReverseCountDown(action.Position, GameState.DELAY_COUNT));
                    break;
            }
        }

        private static void ExecuteDelayReverse(GameState state, Position pos)
        {
            if (state.Board[pos.Row, pos.Col] == StoneColor.None) return;

            StoneColor afterColor = state.Board[pos.Row, pos.Col].Opponent();
            var targets = FindReversePos(state, afterColor, pos);

            FlipStone(state, pos);

            foreach (var p in targets)
            {
                FlipStone(state, p);
            }
        }

        private static void ExecuteReverseEffect(GameState state, Position pos)
        {
            StoneColor afterColor = state.Board[pos.Row, pos.Col].Opponent();
            var targets = FindReversePos(state, afterColor, pos);

            FlipStone(state, pos);

            foreach (var p in targets)
            {
                FlipStone(state, p);
            }
        }

        private static void FlipStone(GameState state, Position pos)
        {
            if (state.StoneTypes[pos.Row, pos.Col] == StoneType.Frozen) return;

            StoneColor original = state.Board[pos.Row, pos.Col];
            state.Board[pos.Row, pos.Col] = original.Opponent();
            state.StoneCount[original]--;
            state.StoneCount[original.Opponent()]++;
        }

        private static bool HasValidMove(GameState state)
        {
            StoneColor currentPlayer = state.CurrentPlayer;
            int size = state.CurrentBoardSize;
            int center = GameState.MAX_BOARD_SIZE / 2;
            int halfSize = size / 2;
            int min = center - halfSize;
            int max = center + halfSize;

            foreach (var kvp in state.Inventories[currentPlayer].AvailableCount)
            {
                if (kvp.Value <= 0) continue;
                bool isReverse = kvp.Key.IsReverseType();

                for (int r = min; r < max; r++)
                {
                    for (int c = min; c < max; c++)
                    {
                        if (state.Board[r, c] != StoneColor.None) continue;
                        Position pos = _positionCache[r, c];

                        if (CanReverse(state, currentPlayer, pos, size)) return true;
                        if (isReverse && CanReverse(state, currentPlayer.Opponent(), pos, size)) return true;
                    }
                }
            }
            return false;
        }

        #region Helper Methods

        private static bool CanReverse(GameState state, StoneColor putColor, Position putPos, int size)
        {
            foreach (var d in Dirs)
            {
                if (CanReverseInDir(state, putColor, putPos, d.Row, d.Col, size)) return true;
            }
            return false;
        }

        private static bool CanReverseInDir(GameState state, StoneColor putColor, Position putPos, int dRow, int dCol, int size)
        {
            int r = putPos.Row + dRow;
            int c = putPos.Col + dCol;
            bool foundOpponent = false;

            int center = GameState.MAX_BOARD_SIZE / 2;
            int half = size / 2;
            int min = center - half;
            int max = center + half;

            while (r >= min && r < max && c >= min && c < max)
            {
                StoneColor stone = state.Board[r, c];
                if (stone == StoneColor.None) return false;
                if (stone == putColor) return foundOpponent;

                foundOpponent = true;
                r += dRow;
                c += dCol;
            }
            return false;
        }

        private static List<Position> FindReversePos(GameState state, StoneColor putColor, Position putPos)
        {
            var list = new List<Position>(16);
            foreach (var d in Dirs)
            {
                FindReversePosInDir(state, putColor, putPos, d.Row, d.Col, list);
            }
            return list;
        }

        private static void FindReversePosInDir(GameState state, StoneColor putColor, Position putPos, int dRow, int dCol, List<Position> result)
        {
            int r = putPos.Row + dRow;
            int c = putPos.Col + dCol;
            int count = 0;
            int size = state.CurrentBoardSize;
            int center = GameState.MAX_BOARD_SIZE / 2;
            int half = size / 2;
            int min = center - half;
            int max = center + half;

            while (r >= min && r < max && c >= min && c < max)
            {
                StoneColor stone = state.Board[r, c];
                if (stone == StoneColor.None) return;

                if (stone == putColor)
                {
                    if (count > 0)
                    {
                        for (int i = 1; i <= count; i++)
                        {
                            result.Add(_positionCache[putPos.Row + dRow * i, putPos.Col + dCol * i]);
                        }
                    }
                    return;
                }
                count++;
                r += dRow;
                c += dCol;
            }
        }

        public static bool IsInBoard(int row, int col, int currentBoardSize)
        {
            int center = GameState.MAX_BOARD_SIZE / 2;
            int halfSize = currentBoardSize / 2;
            return row >= center - halfSize && row < center + halfSize &&
                   col >= center - halfSize && col < center + halfSize;
        }

        #endregion
    }
}
