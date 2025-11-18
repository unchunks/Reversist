using System;
using System.Collections.Generic;

namespace App.Reversi.AI
{
    public class MCTSNode
    {
        public GameState State { get; }
        public MCTSNode Parent { get; }
        public GameAction Action { get; } // 親からこのノードに遷移したときの手
        public List<MCTSNode> Children { get; private set; } // プロパティとして公開
        public List<GameAction> UntriedActions { get; }
        public StoneColor PlayerColor { get; } // このノードの手番プレイヤー

        public int VisitCount { get; private set; }
        public double TotalScore { get; private set; }

        public MCTSNode(GameState state, StoneColor playerColor, MCTSNode parent = null, GameAction action = null)
        {
            State = state;
            Parent = parent;
            Action = action;
            PlayerColor = playerColor;
            Children = new List<MCTSNode>();
            UntriedActions = ReversiSimulator.GetValidActions(State);
            VisitCount = 0;
            TotalScore = 0;
        }

        public bool IsLeaf()
        {
            return Children.Count == 0;
        }

        public bool IsTerminal()
        {
            return State.IsGameOver;
        }

        public bool HasUntriedActions()
        {
            return UntriedActions.Count > 0;
        }

        public int GetUntriedActionsCount()
        {
            return UntriedActions.Count;
        }

        public int GetChildrenCount()
        {
            return Children.Count;
        }

        public MCTSNode Expand(Random random)
        {
            // 未試行の手からランダムに1つ選ぶ
            int index = random.Next(UntriedActions.Count);
            GameAction action = UntriedActions[index];
            UntriedActions.RemoveAt(index);

            // 新しい状態を作成
            GameState nextState = ReversiSimulator.ExecuteAction(State, action);

            // 新しいノードを作成（次は相手の番）
            MCTSNode childNode = new MCTSNode(nextState, PlayerColor.Opponent(), this, action);
            Children.Add(childNode);

            return childNode;
        }

        public void Update(double result)
        {
            VisitCount++;
            TotalScore += result;
        }

        // UCB1値が最大の子ノードを選択
        public MCTSNode SelectBestChildUCB1(double c)
        {
            MCTSNode bestChild = null;
            double bestUCB1 = double.MinValue;

            foreach (var child in Children)
            {
                double ucb1 = (child.TotalScore / child.VisitCount) +
                              c * Math.Sqrt(Math.Log(VisitCount) / child.VisitCount);

                if (ucb1 > bestUCB1)
                {
                    bestUCB1 = ucb1;
                    bestChild = child;
                }
            }

            return bestChild;
        }

        // 最も訪問回数が多い子ノードを取得（最終的な手の選択用）
        public MCTSNode GetMostVisitedChild()
        {
            MCTSNode bestChild = null;
            int maxVisits = -1;

            foreach (var child in Children)
            {
                if (child.VisitCount > maxVisits)
                {
                    maxVisits = child.VisitCount;
                    bestChild = child;
                }
            }

            return bestChild;
        }
    }
}
