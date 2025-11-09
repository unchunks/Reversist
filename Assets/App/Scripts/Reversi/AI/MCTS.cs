using System.Collections.Generic;
using System.Diagnostics;

namespace App.Reversi.AI
{
	public static class MCTS
	{
		/// <summary>
		/// 指定された状態からMCTSを実行し、最善の手を返す
		/// </summary>
		/// <param name="initialState">現在のゲーム状態</param>
		/// <param name="milliseconds">思考時間（ミリ秒）</param>
		public static GameAction Search(GameState initialState, int milliseconds)
		{
			Stopwatch sw = Stopwatch.StartNew();
			MCTSNode rootNode = new MCTSNode(initialState);

			// 有効な手が1つしかない場合は、探索せずに即座に返す
			var validActions = rootNode.HasUntriedActions() ? ReversiSimulator.GetValidActions(initialState) : new List<GameAction>();
			if (validActions.Count == 1)
			{
				return validActions[0];
			}
			// 有効な手がない（パス）場合は、nullを返す
			if (validActions.Count == 0)
			{
				return null;
			}

			// 制限時間いっぱいまでシミュレーションを繰り返す
			while (sw.ElapsedMilliseconds < milliseconds)
			{
				// --- Selection (選択) ---
				MCTSNode node = rootNode;
				while (!node.HasUntriedActions() && !node.State.IsGameOver)
				{
					node = node.SelectBestChild();
				}

				// --- Expansion (展開) ---
				if (node.HasUntriedActions() && !node.State.IsGameOver)
				{
					node = node.Expand(); // 新しい子ノードを作成
				}

				// --- Simulation (シミュレーション) ---
				float result = node.State.IsGameOver
					? ReversiSimulator.GetResult(node.State)
					: node.Simulate(); // CancellationToken.None で実行

				// --- Backpropagation (逆伝播) ---
				node.Backpropagate(result);
			}

			sw.Stop();

			MCTSNode bestChild = rootNode.GetMostVisitedChild();
			return bestChild?.Action;
		}
	}
}
