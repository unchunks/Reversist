//using System.Collections.Generic;
//using System.Diagnostics;

//namespace App.Reversi.AI
//{
//	public static class MCTS
//	{
//		/// <summary>
//		/// 指定された状態からMCTSを実行し、最善の手を返す
//		/// </summary>
//		/// <param name="initialState">現在のゲーム状態</param>
//		/// <param name="milliseconds">思考時間（ミリ秒）</param>
//		public static GameAction Search(GameState initialState, int milliseconds)
//		{
//			Stopwatch sw = Stopwatch.StartNew();
//			MCTSNode rootNode = new MCTSNode(initialState);

//			// 制限時間いっぱいまでシミュレーションを繰り返す
//			while (sw.ElapsedMilliseconds < milliseconds)
//			{
//				// --- Selection (選択) ---
//				// すでに展開済みのノードの端まで降りていく
//				MCTSNode node = rootNode;
//				while (!node.HasUntriedActions() && !node.State.IsGameOver)
//				{
//					node = node.SelectBestChild();
//				}

//				// --- Expansion (展開) ---
//				if (node.HasUntriedActions() && !node.State.IsGameOver)
//				{
//					node = node.Expand(); // 新しい子ノードを作成
//				}

//				// --- Simulation (シミュレーション) ---
//				// もし展開したノードがゲーム終了状態なら、シミュレーションは不要
//				float result = node.State.IsGameOver
//					? ReversiSimulator.GetResult(node.State)
//					: node.Simulate();

//				// --- Backpropagation (逆伝播) ---
//				node.Backpropagate(result);
//			}

//			sw.Stop();

//			// 最も訪問回数が多かった（＝最も有望と判断された）子ノードの手を返す
//			MCTSNode bestChild = rootNode.GetMostVisitedChild();
//			return bestChild.Action;
//		}
//	}
//}
