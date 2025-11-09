using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace App.Reversi.AI
{
	/// <summary>
	/// クラシックMCTSのノード。ランダム・プレイアウトで使用する。
	/// </summary>
	public class MCTSNode_Classic
	{
		private static readonly double C = Math.Sqrt(2.0);
		private static readonly Random _random = new Random();

		public GameState State { get; }
		public GameAction Action { get; }
		public MCTSNode_Classic Parent { get; }
		public List<MCTSNode_Classic> Children { get; }

		public int VisitCount { get; private set; }
		private List<GameAction> _untriedActions;
		private double _winScore;
		private readonly object _lock = new object();

		public MCTSNode_Classic(GameState state, MCTSNode_Classic parent = null, GameAction action = null)
		{
			State = state;
			Parent = parent;
			Action = action;
			Children = new List<MCTSNode_Classic>();
			VisitCount = 0;
			_winScore = 0.0;

			if (!state.IsGameOver)
			{
				_untriedActions = ReversiSimulator.GetValidActions(state);
			}
			else
			{
				_untriedActions = new List<GameAction>();
			}
		}

		public double GetUCB1Score()
		{
			lock (_lock)
			{
				if (VisitCount == 0) return double.PositiveInfinity;
				if (Parent == null) return double.NegativeInfinity;

				return (_winScore / VisitCount) + C * Math.Sqrt(Math.Log(Parent.VisitCount) / VisitCount);
			}
		}

		public MCTSNode_Classic SelectBestChild()
		{
			lock (_lock)
			{
				return Children.OrderByDescending(c => c.GetUCB1Score()).First();
			}
		}

		public MCTSNode_Classic Expand()
		{
			lock (_lock)
			{
				if (!HasUntriedActions())
					throw new InvalidOperationException("未試行の手がないノードでExpandが呼ばれました。");

				GameAction action = _untriedActions[_random.Next(_untriedActions.Count)];
				_untriedActions.Remove(action);

				// 「ディープコピー版」を呼び出し、新しいGameStateを作成する
				GameState nextState = ReversiSimulator.ExecuteAction(State, action);

				MCTSNode_Classic childNode = new MCTSNode_Classic(nextState, this, action);
				Children.Add(childNode);

				return childNode;
			}
		}

		/// <summary>
		/// このノードからランダムにゲームをシミュレート（プレイアウト）し、結果を返す (Simulation)
		/// </summary>
		public float Simulate(CancellationToken token)
		{
			// 1回だけディープコピーする
			GameState simState = new GameState(State);

			while (!simState.IsGameOver)
			{
				if (token.IsCancellationRequested) return 0.0f;

				// キャッシュされた有効な手を取得
				var actions = ReversiSimulator.GetValidActions(simState);

				GameAction randomAction = (actions.Count > 0)
					? actions[_random.Next(actions.Count)]
					: null; // パス

				// 「直接変更版」を呼び出し、simStateを直接書き換える
				ReversiSimulator.ExecuteActionInPlace(simState, randomAction);
			}

			return ReversiSimulator.GetResult(simState);
		}

		public void Backpropagate(float result)
		{
			MCTSNode_Classic node = this;
			while (node != null)
			{
				lock (node._lock)
				{
					node.VisitCount++;
					if (node.State.CurrentPlayer == StoneColor.Black)
					{
						if (result == 1.0f) node._winScore += 1.0;
						else if (result == 0.5f) node._winScore += 0.5;
					}
					else if (node.State.CurrentPlayer == StoneColor.White)
					{
						if (result == -1.0f) node._winScore += 1.0;
						else if (result == 0.5f) node._winScore += 0.5;
					}
				}
				node = node.Parent;
			}
		}

		public bool IsLeafNode() => Children.Count == 0;
		public bool HasUntriedActions()
		{
			lock (_lock)
			{
				return _untriedActions.Count > 0;
			}
		}
		public MCTSNode_Classic GetMostVisitedChild()
		{
			lock (_lock)
			{
				if (Children.Count == 0) return null;
				return Children.OrderByDescending(c => c.VisitCount).FirstOrDefault();
			}
		}
		public int GetVisitCount() => VisitCount;
	}
}
