using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Reversi.AI
{
    // ============================================================================
    // MCTSノードクラス
    // ============================================================================
    public class MCTSNode
    {
        public GameState State { get; }
        public GameAction Action { get; }
        public MCTSNode Parent { get; }
        public int Visits { get; private set; }
        public double TotalScore { get; private set; }

        private List<MCTSNode> _children;
        private List<GameAction> _untriedActions;
        private readonly StoneColor _aiColor;

        public MCTSNode(GameState state, StoneColor aiColor, MCTSNode parent = null, GameAction action = null)
        {
            State = state;
            Action = action;
            Parent = parent;
            Visits = 0;
            TotalScore = 0.0;
            _aiColor = aiColor;
            _children = new List<MCTSNode>();

            if (!state.IsGameOver)
            {
                _untriedActions = ReversiSimulator.GetValidActions(state);
            }
            else
            {
                _untriedActions = new List<GameAction>();
            }
        }

        /// <summary>
        /// UCB1値に基づいて最良の子ノードを選択
        /// </summary>
        public MCTSNode SelectBestChildUCB1(double explorationConstant)
        {
            if (_children.Count == 0)
            {
                throw new InvalidOperationException("子ノードが存在しません");
            }

            MCTSNode bestChild = null;
            double bestUCB1 = double.NegativeInfinity;

            foreach (var child in _children)
            {
                double ucb1 = child.CalculateUCB1(explorationConstant);
                if (ucb1 > bestUCB1)
                {
                    bestUCB1 = ucb1;
                    bestChild = child;
                }
            }

            return bestChild;
        }

        /// <summary>
        /// UCB1値を計算
        /// </summary>
        private double CalculateUCB1(double c)
        {
            if (Visits == 0) return double.PositiveInfinity;
            if (Parent == null) return double.NegativeInfinity;

            double exploitation = TotalScore / Visits;
            double exploration = c * Math.Sqrt(Math.Log(Parent.Visits) / Visits);

            return exploitation + exploration;
        }

        /// <summary>
        /// 未試行の手を展開
        /// </summary>
        public MCTSNode Expand(Random random)
        {
            if (_untriedActions.Count == 0)
            {
                throw new InvalidOperationException("未試行の手がありません");
            }

            int index = random.Next(_untriedActions.Count);
            GameAction action = _untriedActions[index];
            _untriedActions.RemoveAt(index);

            GameState nextState = ReversiSimulator.ExecuteAction(State, action);
            MCTSNode childNode = new MCTSNode(nextState, _aiColor, this, action);
            _children.Add(childNode);

            return childNode;
        }

        /// <summary>
        /// ノードの統計を更新
        /// </summary>
        public void Update(double result)
        {
            Visits++;
            TotalScore += result;
        }

        /// <summary>
        /// 最も訪問回数が多い子ノードを取得
        /// </summary>
        public MCTSNode GetMostVisitedChild()
        {
            if (_children.Count == 0) return null;

            MCTSNode bestChild = _children[0];
            int maxVisits = bestChild.Visits;

            foreach (var child in _children)
            {
                if (child.Visits > maxVisits)
                {
                    maxVisits = child.Visits;
                    bestChild = child;
                }
            }

            return bestChild;
        }

        public bool IsLeaf() => _children.Count == 0;
        public bool IsTerminal() => State.IsGameOver;
        public bool HasUntriedActions() => _untriedActions.Count > 0;
        public int GetUntriedActionsCount() => _untriedActions.Count;
        public int GetChildrenCount() => _children.Count;
    }
}
