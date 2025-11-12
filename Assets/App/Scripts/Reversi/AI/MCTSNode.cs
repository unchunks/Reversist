using System;
using System.Collections.Generic;

namespace App.Reversi.AI
{
	public class MCTSNode
	{
		private const double C_PUCT = 4.0;
		private const int VIRTUAL_LOSS = 3;

		public GameState State { get; private set; }
		public MCTSNode Parent { get; private set; }
		public Dictionary<GameAction, MCTSNode> Children { get; private set; }

		private int _visitCount;
		private double _totalValue;
		private int _virtualLoss;
		private readonly object _lock = new object();

		private float[] _policy;
		private float _value;
		private bool _isEvaluated;
		private List<GameAction> _validActions;

		public MCTSNode(GameState state, MCTSNode parent = null)
		{
			State = state;
			Parent = parent;
			Children = new Dictionary<GameAction, MCTSNode>();
			_policy = new float[12 * 12 * 5];

			if (!state.IsGameOver)
			{
				_validActions = OptimizedSimulator.GetValidActions(state);
			}
			else
			{
				_validActions = new List<GameAction>();
				_isEvaluated = true;
				_value = OptimizedSimulator.GetResult(state);
				Backpropagate(_value);
			}
		}

		public GameAction SelectActionByPUCT()
		{
			lock (_lock)
			{
				if (_validActions.Count == 0) return null;

				double bestScore = double.NegativeInfinity;
				GameAction bestAction = null;

				foreach (var action in _validActions)
				{
					double score = GetPUCTScore(action);
					if (score > bestScore)
					{
						bestScore = score;
						bestAction = action;
					}
				}
				return bestAction;
			}
		}

		private double GetPUCTScore(GameAction action)
		{
			double qValue = 0.0;
			int childVisits = 0;

			if (Children.TryGetValue(action, out var child))
			{
				childVisits = child._visitCount + child._virtualLoss;
				qValue = childVisits > 0 ? child._totalValue / childVisits : 0.0;
			}

			double priorPolicy = GetPriorPolicyForAction(action);
			double exploration = C_PUCT * priorPolicy * Math.Sqrt(_visitCount + _virtualLoss) / (1.0 + childVisits);

			return -qValue + exploration;
		}

		public MCTSNode Expand(GameAction action)
		{
			lock (_lock)
			{
				if (Children.TryGetValue(action, out var child))
				{
					child.AddVirtualLoss();
					return child;
				}

				var nextState = GameState.GetFromPool();
				nextState.CopyFrom(State);
				OptimizedSimulator.ExecuteActionInPlace(nextState, action);

				var newChild = new MCTSNode(nextState, this);
				Children.Add(action, newChild);
				newChild.AddVirtualLoss();
				return newChild;
			}
		}

		private void AddVirtualLoss() { lock (_lock) { _virtualLoss += VIRTUAL_LOSS; } }
		private void RemoveVirtualLoss() { lock (_lock) { _virtualLoss -= VIRTUAL_LOSS; } }

		public void SetEvaluationResult(float[] policy, float value)
		{
			lock (_lock)
			{
				if (_isEvaluated) return;
				_isEvaluated = true;
				Array.Copy(policy, _policy, policy.Length);
				_value = value;
				RemoveVirtualLoss();
				Backpropagate(value);
			}
		}

		public void Backpropagate(float value)
		{
			MCTSNode node = this;
			while (node != null)
			{
				lock (node._lock)
				{
					node._visitCount++;
					node._totalValue += value;
				}
				value = -value;
				node = node.Parent;
			}
		}

		public bool IsLeafAndNotEvaluated() { lock (_lock) { return Children.Count == 0 && !_isEvaluated; } }
		public bool IsGameOver() => State.IsGameOver;
		public int GetVisitCount() => _visitCount;
		public bool IsEvaluated() => _isEvaluated;

		private float GetPriorPolicyForAction(GameAction action)
		{
			int posIndex = action.Position.Row * 12 + action.Position.Col;
			int typeIndex = (int)action.Type;
			int index = typeIndex * 144 + posIndex;
			return index < _policy.Length ? _policy[index] : 0.0f;
		}
	}
}