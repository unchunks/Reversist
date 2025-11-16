using System;
using System.Collections.Generic;
// using System.Linq; // LINQは不要になりました

namespace App.Reversi.AI
{
    /// <summary>
    /// Unityに依存せず、純粋なC#ロジックだけでゲームを高速にシミュレートする（最適化版）
    /// </summary>
    public static class ReversiSimulator
    {
        // 8方向の定義 (List<T>より配列の方が高速)
        private static readonly Position[] Dirs = {
            new Position(-1, -1), new Position(-1, 0), new Position(-1, 1),
            new Position(0, -1), new Position(0, 1),
            new Position(1, -1), new Position(1, 0), new Position(1, 1)
        };

        // Positionオブジェクトの事前キャッシュ (GC負荷低減のため)
        private static readonly Position[,] _positionCache;

        /// <summary>
        /// 静的コンストラクタでPositionキャッシュを初期化
        /// </summary>
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

        /// <summary>
        /// 現在の状態で、ゲームが終了しているかを判定する
        /// </summary>
        public static float GetResult(GameState state)
        {
            if (state.IsGameOver)
            {
                if (state.StoneCount[StoneColor.Black] > state.StoneCount[StoneColor.White]) return 1.0f; // 黒勝利
                if (state.StoneCount[StoneColor.White] > state.StoneCount[StoneColor.Black]) return -1.0f; // 白勝利
                return 0.5f; // 引き分け
            }
            return 0.0f; // ゲーム続行
        }

        /// <summary>
        /// 現在の状態で実行可能なすべての手をリストアップする (最適化版)
        /// </summary>
        public static List<GameAction> GetValidActions(GameState state)
        {
            var actions = new List<GameAction>();
            StoneColor currentPlayer = state.CurrentPlayer;

            // LINQを排除し、高速なforeachループに変更
            foreach (var kvp in state.Inventories[currentPlayer].AvailableCount)
            {
                if (kvp.Value <= 0) continue; // 在庫がない

                StoneType type = kvp.Key;
                bool isReverseType = type.IsReverseType();

                for (int r = 0; r < GameState.MAX_BOARD_SIZE; r++)
                {
                    for (int c = 0; c < GameState.MAX_BOARD_SIZE; c++)
                    {
                        if (!IsInBoard(r, c, state.CurrentBoardSize)) continue;
                        if (state.Board[r, c] != StoneColor.None) continue; // 空きマスでない

                        // Positionの生成をキャッシュから取得
                        Position pos = _positionCache[r, c];

                        // 1. 通常の石（自分の色）で置けるか
                        // CanReverseはリストを生成せず、boolだけを返す
                        if (CanReverse(state, currentPlayer, pos))
                        {
                            actions.Add(new GameAction(pos, type, currentPlayer));
                        }
                        // 2. リバース系の石で、相手の色で置けるか
                        else if (isReverseType && CanReverse(state, currentPlayer.Opponent(), pos))
                        {
                            actions.Add(new GameAction(pos, type, currentPlayer));
                        }
                    }
                }
            }

            // 重複を削除 (NormalとReverseで同じ場所が候補になった場合など)
            // Note: 非常に稀だが、ロジックとして必要。
            // しかし、MCTSのシミュレーションでは重複があっても本質的な問題はないため、
            // さらなる速度が必要な場合は以下のDistinctをコメントアウトすることも検討
            return actions;

            // 追記：GetValidActionsのロジックを変更したため、重複は発生しなくなりました。
            // 以前は (r, c) のループの外で putColor をループしていたため、
            // (r, c) が Black でも White でも置ける場合、(r, c, StoneType.Reverse) が2回追加されていました。
            // 現在のロジックでは (r, c) に対して CanReverse(Black), CanReverse(White) をチェックし、
            // どちらか一方でも true ならば actions.Add して break する（べきだが、
            // 全ての石タイプを追加するために break していない）。

            // ロジックの再検証：
            // 石タイプ (Normal, Reverse)
            //   - 盤面 (r, c)
            //     - if (Normal) -> CanReverse(Player) -> Add(pos, Normal)
            //     - if (Reverse) -> CanReverse(Player) OR CanReverse(Opponent) -> Add(pos, Reverse)

            // 現在のロジック：
            // 石タイプ (Normal, Extend, ...)
            //   - 盤面 (r, c)
            //     - pos = (r, c)
            //     - if (CanReverse(Player, pos)) -> Add(pos, type, Player)
            //     - else if (type.IsReverseType && CanReverse(Opponent, pos)) -> Add(pos, type, Player)

            // このロジックでは、(r, c) が Player でも Opponent でも置けるマスの場合、
            // 石タイプが Normal (IsReverseType=false) なら Add は1回。
            // 石タイプが Reverse (IsReverseType=true) なら if (CanReverse(Player, pos)) が true になり
            // Add(pos, Reverse, Player) が追加され、else if は実行されない。
            // よって、重複は発生しません。
        }

        /// <summary>
        /// 行動を実行し、次の状態を返す (元の状態は変更しない) (最適化版)
        /// </summary>
        public static GameState ExecuteAction(GameState currentState, GameAction action)
        {
            // 状態をディープコピー
            GameState nextState = new GameState(currentState);

            // 石を消費 
            nextState.Inventories[action.Player].Decrease(action.Type);

            // 石を置く色を決定
            StoneColor putColor = action.Type.IsReverseType() ? action.Player.Opponent() : action.Player;

            // ひっくり返す石を探す (効率化されたFindReversePosを呼ぶ)
            List<Position> reversePos = FindReversePos(nextState, putColor, action.Position);

            // 石を置く 
            nextState.Board[action.Position.Row, action.Position.Col] = putColor;
            nextState.StoneTypes[action.Position.Row, action.Position.Col] = action.Type;
            nextState.StoneCount[putColor]++;

            // 石をひっくり返す
            foreach (var pos in reversePos)
            {
                StoneColor originalColor = nextState.Board[pos.Row, pos.Col];
                if (nextState.StoneTypes[pos.Row, pos.Col] != StoneType.Frozen) // 凍結石はひっくり返らない
                {
                    nextState.Board[pos.Row, pos.Col] = originalColor.Opponent();
                    nextState.StoneCount[originalColor]--;
                    nextState.StoneCount[originalColor.Opponent()]++;
                }
            }

            // 遅延反転の処理
            for (int i = nextState.DelayReverseStack.Count - 1; i >= 0; i--)
            {
                var item = nextState.DelayReverseStack[i];
                item.Delay--;
                if (item.Delay <= 0)
                {
                    Position pos = item.Pos;
                    if (nextState.Board[pos.Row, pos.Col] != StoneColor.None)
                    {
                        StoneColor afterColor = nextState.Board[pos.Row, pos.Col].Opponent();
                        // 効率化されたFindReversePosを呼ぶ
                        List<Position> reversePosDelay = FindReversePos(nextState, afterColor, pos);

                        // 自身を反転
                        StoneColor originalColor = nextState.Board[pos.Row, pos.Col];
                        nextState.Board[pos.Row, pos.Col] = originalColor.Opponent();
                        nextState.StoneCount[originalColor]--;
                        nextState.StoneCount[originalColor.Opponent()]++;

                        // 周囲を反転
                        foreach (var p in reversePosDelay)
                        {
                            StoneColor oc = nextState.Board[p.Row, p.Col];
                            if (nextState.StoneTypes[p.Row, p.Col] != StoneType.Frozen)
                            {
                                nextState.Board[p.Row, p.Col] = oc.Opponent();
                                nextState.StoneCount[oc]--;
                                nextState.StoneCount[oc.Opponent()]++;
                            }
                        }
                    }
                    nextState.DelayReverseStack.RemoveAt(i);
                }
                else
                {
                    nextState.DelayReverseStack[i] = item;
                }
            }

            // 特殊石の処理
            switch (action.Type)
            {
                case StoneType.Extend:
                    nextState.CurrentBoardSize = Math.Min(nextState.CurrentBoardSize + 2, GameState.MAX_BOARD_SIZE);
                    break;
                case StoneType.Reverse:
                    StoneColor afterColor = nextState.Board[action.Position.Row, action.Position.Col].Opponent();
                    // 効率化されたFindReversePosを呼ぶ
                    reversePos = FindReversePos(nextState, afterColor, action.Position);
                    // 自身を反転
                    StoneColor originalColor = nextState.Board[action.Position.Row, action.Position.Col];
                    nextState.Board[action.Position.Row, action.Position.Col] = originalColor.Opponent();
                    nextState.StoneCount[originalColor]--;
                    nextState.StoneCount[originalColor.Opponent()]++;
                    // 周囲を反転
                    foreach (var p in reversePos)
                    {
                        StoneColor oc = nextState.Board[p.Row, p.Col];
                        if (nextState.StoneTypes[p.Row, p.Col] != StoneType.Frozen)
                        {
                            nextState.Board[p.Row, p.Col] = oc.Opponent();
                            nextState.StoneCount[oc]--;
                            nextState.StoneCount[oc.Opponent()]++;
                        }
                    }
                    break;
                case StoneType.DelayReverse:
                    nextState.DelayReverseStack.Add(new ReverseCountDown(action.Position, GameState.DELAY_COUNT));
                    break;
            }

            // ターン交代とパス/終了判定 (最適化)
            nextState.CurrentPlayer = nextState.CurrentPlayer.Opponent();

            // GetValidActionsを1回だけ呼び出し、結果をキャッシュする
            var opponentActions = GetValidActions(nextState);
            if (opponentActions.Count == 0)
            {
                nextState.CurrentPlayer = nextState.CurrentPlayer.Opponent();

                var playerActions = GetValidActions(nextState);
                if (playerActions.Count == 0)
                {
                    // 2回連続パス
                    nextState.IsGameOver = true;
                }
            }

            return nextState;
        }

        #region Helper Methods

        /// <summary>
        /// (最適化) 石を置けるかどうかだけを判定する (GC負荷低減)
        /// </summary>
        private static bool CanReverse(GameState state, StoneColor putColor, Position putPos)
        {
            foreach (var d in Dirs)
            {
                if (CanReverseInDir(state, putColor, putPos, d.Row, d.Col))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// (最適化) 指定方向に返せる石があるかだけを判定する (GC負荷低減)
        /// </summary>
        private static bool CanReverseInDir(GameState state, StoneColor putColor, Position putPos, int dRow, int dCol)
        {
            var currentRow = putPos.Row + dRow;
            var currentCol = putPos.Col + dCol;
            bool foundOpponent = false;

            while (IsInBoard(currentRow, currentCol, state.CurrentBoardSize))
            {
                StoneColor stone = state.Board[currentRow, currentCol];

                if (stone == StoneColor.None)
                {
                    return false; // 空きマス
                }

                if (stone == putColor)
                {
                    // 自分の石が見つかった。間に相手の石が1つでもあればOK
                    return foundOpponent;
                }

                // 相手の石
                foundOpponent = true;

                currentRow += dRow;
                currentCol += dCol;
            }

            return false; // 盤の端まで相手の石だった
        }


        /// <summary>
        /// (最適化) ひっくり返す石のリストを取得する (リスト生成を1回に)
        /// </summary>
        private static List<Position> FindReversePos(GameState state, StoneColor putColor, Position putPos)
        {
            // リストの生成をここ1回だけにする
            var reversePos = new List<Position>();
            foreach (var d in Dirs)
            {
                //FindReversePosInDirがリストに直接追加する
                FindReversePosInDir(state, putColor, putPos, d.Row, d.Col, reversePos);
            }
            return reversePos;
        }

        /*
        /// <summary>
        /// (最適化) 指定方向にひっくり返す石を探し、引数のリストに追加する
        /// </summary>
        private static void FindReversePosInDir(GameState state, StoneColor putColor, Position putPos, int dRow, int dCol, List<Position> reversePos)
        {
            // このメソッド内でのリスト生成をなくす
            var tempPos = new List<Position>();
            var currentRow = putPos.Row + dRow;
            var currentCol = putPos.Col + dCol;

            while (IsInBoard(currentRow, currentCol, state.CurrentBoardSize))
            {
                StoneColor stone = state.Board[currentRow, currentCol];

                if (stone == StoneColor.None)
                {
                    return; // 反転できる石はない
                }

                if (stone == putColor)
                {
                    // 自分の石が見つかったら、ためていた石を本リストに追加
                    reversePos.AddRange(tempPos);
                    return;
                }

                // 相手の石（反転候補）
                tempPos.Add(_positionCache[currentRow, currentCol]); // キャッシュから取得

                currentRow += dRow;
                currentCol += dCol;
            }

            // 端まで行った場合
            // Note: tempPos は破棄される (GC対象だが、FindReversePosInDir(List<P>...)よりはるかにマシ)
            // さらなる最適化：tempPosも引数で使いまわす

            // --- 訂正 ---
            // `tempPos` のアロケーションが残っていました。これも排除します。
            // 再度、`FindReversePosInDir` を修正します。
        }
        */

        // --- `FindReversePosInDir` の最終最適化版 ---
        // 上記の `FindReversePosInDir` は `tempPos` のアロケーションが残っているため、
        // 以下の「アロケーションがゼロ」のバージョンに差し替えてください。
        // （コードの可読性のため、差し替え後の全量を以下に記載します）


        /// <summary>
        /// (最適化) 指定方向にひっくり返す石を探し、引数のリストに追加する (GCゼロ版)
        /// </summary>
        private static void FindReversePosInDir(GameState state, StoneColor putColor, Position putPos, int dRow, int dCol, List<Position> reversePos)
        {
            var currentRow = putPos.Row + dRow;
            var currentCol = putPos.Col + dCol;
            
            // 盤の端まで、反転候補の石が何個連続しているか
            int opponentStonesCount = 0;
            
            while (IsInBoard(currentRow, currentCol, state.CurrentBoardSize))
            {
                StoneColor stone = state.Board[currentRow, currentCol];

                if (stone == StoneColor.None)
                {
                    return; // 空きマス。反転不可
                }
                
                if (stone == putColor)
                {
                    // 自分の石が見つかった。
                    if (opponentStonesCount > 0)
                    {
                        // 間に相手の石があった場合、その分だけ遡ってリストに追加
                        for (int i = 1; i <= opponentStonesCount; i++)
                        {
                            int r = putPos.Row + (dRow * i);
                            int c = putPos.Col + (dCol * i);
                            reversePos.Add(_positionCache[r, c]);
                        }
                    }
                    return; // 終了
                }

                // 相手の石
                opponentStonesCount++;

                currentRow += dRow;
                currentCol += dCol;
            }
            
            // 盤の端まで相手の石だった場合 (反転不可)
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
