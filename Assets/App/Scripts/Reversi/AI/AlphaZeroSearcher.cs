using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace App.Reversi.AI
{
	/// <summary>
	/// AlphaZero MCTSの検索（思考）ルーチンを独立させたクラス
	/// AIAgent から呼び出される
	/// </summary>
	public static class AlphaZeroSearcher
	{
		public static async UniTask<GameAction> FindBestMove(
			GameState initialState,
			int iterations,
			CancellationToken token)
		{
			MCTSNode_AlphaZero rootNode = new MCTSNode_AlphaZero(initialState);

			if (NNEvaluatorService.Instance == null)
			{
				UnityEngine.Debug.LogError("NNEvaluatorServiceが利用できません。");
				return null;
			}

			NNEvaluatorService.Instance.EnqueueNode(rootNode);
			await UniTask.WaitUntil(() => rootNode.IsEvaluated(), cancellationToken: token);

			for (int i = 0; i < iterations; i++)
			{
				if (token.IsCancellationRequested) break;
				MCTSNode_AlphaZero node = rootNode;

				while (!node.IsLeafAndNotEvaluated() && !node.IsGameOver())
				{
					GameAction action = node.SelectActionByPUCT();
					if (action == null) break;
					node = node.Expand(action);
				}

				if (node.IsLeafAndNotEvaluated())
				{
					NNEvaluatorService.Instance.EnqueueNode(node);
				}
			}

			if (token.IsCancellationRequested) return null;

			if (rootNode.Children.Count == 0)
			{
				UnityEngine.Debug.LogWarning("AIの思考結果、有効な手がありませんでした（パス）。");
				return null;
			}

			GameAction bestAction = rootNode.Children
				.OrderByDescending(kvp => kvp.Value.GetVisitCount())
				.First().Key;

			return bestAction;
		}
	}
}
