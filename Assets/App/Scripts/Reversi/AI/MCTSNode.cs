using System;
using System.Collections.Generic;
using System.Diagnostics;

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
                _validActions = ReversiSimulator.GetValidActions(state);
            }
            else
            {
                _validActions = new List<GameAction>();
                _isEvaluated = true;
                _value = ReversiSimulator.GetResult(state);
                Backpropagate(_value);
            }
        }

        public GameAction SelectActionByPUCT()
        {
            lock (_lock)
            {
                //UnityEngine.Debug.Log($"Valid actions count: {_validActions.Count}");
                if (_validActions.Count == 0) return null;

                double bestScore = double.NegativeInfinity;
                GameAction bestAction = null;

                foreach (var action in _validActions)
                {
                    double score = GetPUCTScore(action);
                    //UnityEngine.Debug.Log("Action: " + action + ", PUCT Score: " + score);
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

            //UnityEngine.Debug.Log($"Action: {action}, Q-Value: {qValue}, Prior: {priorPolicy}, Exploration: {exploration}, VisitCount: {_visitCount}, VirtualLoss: {_virtualLoss}, ChildVisits: {childVisits}");

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
                ReversiSimulator.ExecuteActionInPlace(nextState, action);

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

                if (Parent != null)
                {
                    RemoveVirtualLoss();
                }

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

            float prior = index < _policy.Length ? _policy[index] : 0.0f;

            // NaN および Infinity をチェック
            if (float.IsNaN(prior) || float.IsInfinity(prior) || prior < 0)
            {
                UnityEngine.Debug.LogError($"[Debug] Prior policy for action {action} is invalid ({prior}). Setting to 0.0f.");
                return 0.0f;
            }
            // 異常に大きな値（> 1.0）の警告を追加（Softmaxが機能しているかの確認）
            if (prior > 1.0f)
            {
                UnityEngine.Debug.LogWarning($"[Debug] Prior policy for action {action} is > 1.0 ({prior}). NN output may be broken.");
                // MCTSの動作を継続させるため、あえて1.0fにクランプする手段も考えられるが、
                // 現状はNNのデバッグのためそのまま返すか、0.0fとして無視するのが良い。
            }

            return prior;
        }
    }
}