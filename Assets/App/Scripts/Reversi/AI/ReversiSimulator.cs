using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Reversi.AI
{
	/// <summary>
	/// Unityに依存せず、純粋なC#ロジックだけでゲームを高速にシミュレートする
	/// </summary>
	public static class ReversiSimulator
	{
		// 8方向の定義
		private static readonly List<Position> Dirs = new List<Position> {
			new Position(-1, -1), new Position(-1, 0), new Position(-1, 1),
			new Position(0, -1), new Position(0, 1),
			new Position(1, -1), new Position(1, 0), new Position(1, 1)
		};

		/// <summary>
		/// 現在の状態で、ゲームが終了しているかを判定する
		/// </summary>
		/// <returns>終了していなければ 0, 黒勝利なら 1, 白勝利なら -1, 引き分けなら 0.5</returns>
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
		/// GetValidActionsの内部計算ロジック（キャッシュなし）
		/// </summary>
		private static List<GameAction> GetValidActionsInternal(GameState state)
		{
			var actions = new List<GameAction>();
			var availableStoneTypes = state.Inventories[state.CurrentPlayer]
				.Where(kvp => kvp.Value > 0) // 在庫が1以上
				.Select(kvp => kvp.Key);     // の石タイプ

			foreach (var type in availableStoneTypes)
			{
				// 石の種類ごとに置ける場所を探す
				var putColors = new List<StoneColor> { state.CurrentPlayer };
				if (type.IsReverseType())
				{
					putColors.Add(state.CurrentPlayer.Opponent());
				}

				for (int r = 0; r < GameState.MAX_BOARD_SIZE; r++)
				{
					for (int c = 0; c < GameState.MAX_BOARD_SIZE; c++)
					{
						if (!IsInBoard(r, c, state.CurrentBoardSize)) continue;
						if (state.Board[r, c] != StoneColor.None) continue; // 空きマスでない

						var pos = new Position(r, c);
						foreach (var putColor in putColors)
						{
							if (FindReversePos(state, putColor, pos).Count > 0)
							{
								actions.Add(new GameAction(pos, type, state.CurrentPlayer));
								break; // このマスには置けることが確定
							}
						}
					}
				}
			}

			return actions;
		}

		/// <summary>
		/// 現在の状態で実行可能なすべての手をリストアップする
		/// </summary>
		public static List<GameAction> GetValidActions(GameState state)
		{
			if (state.ValidActionsCache != null)
			{
				return state.ValidActionsCache; // 計算済みのキャッシュを返す
			}

			return GetValidActionsInternal(state);
		}

		/// <summary>
		/// 行動を実行し、次の状態を返す (元の状態は変更しない)（ディープコピー版）
		/// MCTSNodeのExpand（ノード展開）で使用
		/// </summary>
		public static GameState ExecuteAction(GameState currentState, GameAction action)
		{
			// 状態をディープコピー
			GameState nextState = new GameState(currentState);

			// コピーした状態を「直接変更」する
			ExecuteActionInPlace(nextState, action);

			return nextState;
		}

		/// <summary>
		/// 行動を実行し、渡されたGameStateを「直接変更」する（高速版）
		/// MCTSのSimulate（プレイアウト）で使用
		/// </summary>
		public static void ExecuteActionInPlace(GameState stateToModify, GameAction action)
		{
			// 石を消費 (actionがnull（パス）の場合は消費しない)
			if (action != null)
			{
				// GameStateのコンストラクタでInventoriesがディープコピーされているため、この変更は安全
				stateToModify.Inventories[action.Player].Decrease(action.Type);

				// 石を置く色を決定
				StoneColor putColor = action.Player;
				if (action.Type.IsReverseType())
				{
					putColor = action.Player.Opponent();
				}

				// ひっくり返す石を探す
				List<Position> reversePos = FindReversePos(stateToModify, putColor, action.Position);

				// 石を置く
				stateToModify.Board[action.Position.Row, action.Position.Col] = putColor;
				stateToModify.StoneTypes[action.Position.Row, action.Position.Col] = action.Type;
				stateToModify.StoneCount[putColor]++;

				// 石をひっくり返す
				foreach (var pos in reversePos)
				{
					StoneColor originalColor = stateToModify.Board[pos.Row, pos.Col];
					if (stateToModify.StoneTypes[pos.Row, pos.Col] != StoneType.Frozen)
					{
						stateToModify.Board[pos.Row, pos.Col] = originalColor.Opponent();
						stateToModify.StoneCount[originalColor]--;
						stateToModify.StoneCount[originalColor.Opponent()]++;
					}
				}

				// 遅延反転の処理
				for (int i = stateToModify.DelayReverseStack.Count - 1; i >= 0; i--)
				{
					var item = stateToModify.DelayReverseStack[i];
					item.Delay--;
					if (item.Delay <= 0)
					{
						Position pos = item.Pos;
						if (stateToModify.Board[pos.Row, pos.Col] != StoneColor.None)
						{
							StoneColor afterColor = stateToModify.Board[pos.Row, pos.Col].Opponent();
							List<Position> reversePosDelay = FindReversePos(stateToModify, afterColor, pos);

							StoneColor originalColor = stateToModify.Board[pos.Row, pos.Col];
							// 自身を反転
							if (stateToModify.StoneTypes[pos.Row, pos.Col] != StoneType.Frozen) // 念のためチェック
							{
								stateToModify.Board[pos.Row, pos.Col] = originalColor.Opponent();
								stateToModify.StoneCount[originalColor]--;
								stateToModify.StoneCount[originalColor.Opponent()]++;
							}

							// 周囲を反転
							foreach (var p in reversePosDelay)
							{
								StoneColor oc = stateToModify.Board[p.Row, p.Col];
								if (stateToModify.StoneTypes[p.Row, p.Col] != StoneType.Frozen)
								{
									stateToModify.Board[p.Row, p.Col] = oc.Opponent();
									stateToModify.StoneCount[oc]--;
									stateToModify.StoneCount[oc.Opponent()]++;
								}
							}
						}
						stateToModify.DelayReverseStack.RemoveAt(i);
					}
					else
					{
						stateToModify.DelayReverseStack[i] = item;
					}
				}

				// 特殊石の処理
				switch (action.Type)
				{
					case StoneType.Extend:
						stateToModify.CurrentBoardSize = Math.Min(stateToModify.CurrentBoardSize + 2, GameState.MAX_BOARD_SIZE);
						break;
					case StoneType.Reverse:
						StoneColor afterColor = stateToModify.Board[action.Position.Row, action.Position.Col].Opponent();
						List<Position> reversePosReverse = FindReversePos(stateToModify, afterColor, action.Position);
						StoneColor originalColor = stateToModify.Board[action.Position.Row, action.Position.Col];
						// 自身を反転
						stateToModify.Board[action.Position.Row, action.Position.Col] = originalColor.Opponent();
						stateToModify.StoneCount[originalColor]--;
						stateToModify.StoneCount[originalColor.Opponent()]++;
						// 周囲を反転
						foreach (var p in reversePosReverse)
						{
							StoneColor oc = stateToModify.Board[p.Row, p.Col];
							if (stateToModify.StoneTypes[p.Row, p.Col] != StoneType.Frozen)
							{
								stateToModify.Board[p.Row, p.Col] = oc.Opponent();
								stateToModify.StoneCount[oc]--;
								stateToModify.StoneCount[oc.Opponent()]++;
							}
						}
						break;
					case StoneType.DelayReverse:
						stateToModify.DelayReverseStack.Add(new ReverseCountDown(action.Position, GameState.DELAY_COUNT));
						break;
				}
			}

			// ターン交代とパス/終了判定
			stateToModify.CurrentPlayer = stateToModify.CurrentPlayer.Opponent();

			// 「次の手」を計算
			List<GameAction> nextActions = GetValidActionsInternal(stateToModify);

			if (nextActions.Count == 0)
			{
				stateToModify.CurrentPlayer = stateToModify.CurrentPlayer.Opponent();

				nextActions = GetValidActionsInternal(stateToModify);
				if (nextActions.Count == 0)
				{
					stateToModify.IsGameOver = true;
				}
			}

			// 計算結果をキャッシュに保存
			stateToModify.ValidActionsCache = nextActions;
		}

		#region Helper Methods

		private static List<Position> FindReversePos(GameState state, StoneColor putColor, Position putPos)
		{
			var reversePos = new List<Position>();
			foreach (var d in Dirs)
			{
				reversePos.AddRange(FindReversePosInDir(state, putColor, putPos, d.Row, d.Col));
			}
			return reversePos;
		}

		private static List<Position> FindReversePosInDir(GameState state, StoneColor putColor, Position putPos, int dRow, int dCol)
		{
			var reversePos = new List<Position>();
			var currentRow = putPos.Row + dRow;
			var currentCol = putPos.Col + dCol;
			while (IsInBoard(currentRow, currentCol, state.CurrentBoardSize))
			{
				if (state.Board[currentRow, currentCol] == StoneColor.None)
				{
					break;
				}
				if (state.Board[currentRow, currentCol] == putColor)
				{
					return reversePos;
				}
				reversePos.Add(new Position(currentRow, currentCol));
				currentRow += dRow;
				currentCol += dCol;
			}
			return new List<Position>();
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