using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Reversi.AI
{
    /// <summary>
    /// モンテカルロ木探索の各ノード
    /// </summary>
    public class MCTSNode
    {
        // UCB1計算用の探索定数 (sqrt(2) が一般的)
        private static readonly double C = Math.Sqrt(2.0);
        private static readonly Random _random = new Random();

        public GameState State { get; }
        public GameAction Action { get; } // このノードに至った親からの行動
        public MCTSNode Parent { get; }
        public List<MCTSNode> Children { get; }

        public int VisitCount { get; private set; }
        private List<GameAction> _untriedActions; // まだ試していない有効な手
        private double _winScore; // AI視点での勝利スコア

        /// <summary>
        /// ノードのコンストラクタ
        /// </summary>
        public MCTSNode(GameState state, MCTSNode parent = null, GameAction action = null)
        {
            State = state;
            Parent = parent;
            Action = action;
            Children = new List<MCTSNode>();

            VisitCount = 0;
            _winScore = 0.0;

            // この状態で実行可能な手をシミュレーターから取得する
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
        /// UCB1 (Upper Confidence Bound 1) スコアを計算する
        /// 訪問回数が0の場合は無限大を返し、優先的に選択されるようにする
        /// </summary>
        public double GetUCB1Score()
        {
            if (VisitCount == 0)
            {
                return double.PositiveInfinity; // 未訪問のノードを最優先
            }
            if (Parent == null)
            {
                return double.NegativeInfinity; // ルートノードは選択対象外
            }

            // (自分の平均勝率) + C * sqrt( (親の訪問回数の対数) / (自分の訪問回数) )
            return (_winScore / VisitCount) + C * Math.Sqrt(Math.Log(Parent.VisitCount) / VisitCount);
        }

        /// <summary>
        /// UCB1スコアが最も高い子ノードを選択する (Selection)
        /// </summary>
        /// <returns>子ノードがない場合は例外を返す</returns>
        public MCTSNode SelectBestChild()
        {
            // 子ノードがない場合は例外を返す
            return Children.OrderByDescending(c => c.GetUCB1Score()).First();
        }

        /// <summary>
        /// 未試行の手を一つ展開し、新しい子ノードを作成する
        /// </summary>
        public MCTSNode Expand()
        {
            if (!HasUntriedActions())
                throw new InvalidOperationException("未試行の手がないノードでExpandが呼ばれました。");

            // 未試行の手からランダムに1つ選び、未試行リストから削除
            GameAction action = _untriedActions[_random.Next(_untriedActions.Count)];
            _untriedActions.Remove(action);

            // その手を実行した次の状態をシミュレーターで計算
            GameState nextState = ReversiSimulator.ExecuteAction(State, action);

            // 新しい子ノードを作成してツリーに追加
            MCTSNode childNode = new MCTSNode(nextState, this, action);
            Children.Add(childNode);

            return childNode;
        }

        /// <summary>
        /// このノードからランダムにゲームをシミュレートし、結果を返す
        /// 改善点がかなり多い
        /// </summary>
        public float Simulate()
        {
            GameState simState = new GameState(State); // 現在の状態をコピーしてシミュレーション開始

            // 深すぎるシミュレーションを防ぐ
            int simulationDepth = 0;
            const int MAX_SIMULATION_DEPTH = 20;

            while (!simState.IsGameOver && simulationDepth < MAX_SIMULATION_DEPTH)
            {
                simulationDepth++;
                // 有効な手をすべて取得
                var actions = ReversiSimulator.GetValidActions(simState);

                if (actions.Count > 0)
                {
                    // --- 貪欲法による手の選択 ---
                    GameAction bestAction = null;
                    double bestScore;

                    // 現在のシミュレーション状態をチェック
                    bool is10x10 = simState.CurrentBoardSize == 10;
                    bool aiHasExtend = simState.Inventories[StoneColor.Black].AvailableCount[StoneType.Extend] > 0;
                    bool opponentHasExtend = simState.Inventories[StoneColor.White].AvailableCount[StoneType.Extend] > 0;

                    // Expandを相手が使い(在庫0)、自分が使っていない(在庫1)、10x10盤面
                    bool strategicConditionMet = is10x10 && aiHasExtend && !opponentHasExtend;

                    // 黒(AI)のターンの場合、評価値が「最大」になる手を探す
                    if (simState.CurrentPlayer == StoneColor.Black)
                    {
                        bestScore = double.NegativeInfinity;
                        foreach (var action in actions)
                        {
                            if (strategicConditionMet && action.Type == StoneType.Extend)
                            {
                                // この状況でExtendを使う手は最悪とみなす
                                // 評価を実行せず、スコアを最低にする
                                bestScore = -999999.0;
                                bestAction = action;
                                break; // この手（Extend）が選ばれることはほぼなくなる
                            }

                            GameState next = ReversiSimulator.ExecuteAction(simState, action);
                            double score = ReversiEvaluator.Evaluate(next);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestAction = action;
                            }
                        }
                    }
                    // 白(人間)のターンの場合、評価値が「最小」になる手を探す
                    else
                    {
                        bestScore = double.PositiveInfinity;
                        foreach (var action in actions)
                        {
                            if (strategicConditionMet && action.Type == StoneType.Extend)
                            {
                                // 評価を実行せず、スコアを最低にする
                                bestScore = 999999.0;
                                bestAction = action;
                                break;
                            }

                            GameState next = ReversiSimulator.ExecuteAction(simState, action);
                            double score = ReversiEvaluator.Evaluate(next);
                            if (score < bestScore)
                            {
                                bestScore = score;
                                bestAction = action;
                            }
                        }
                    }

                    simState = ReversiSimulator.ExecuteAction(simState, bestAction);
                }
                else
                {
                    // パス
                }
            }

            if (!simState.IsGameOver)
            {
                double finalScore = ReversiEvaluator.Evaluate(simState);
                if (finalScore > 100) return 1.0f;  // 黒がかなり有利
                if (finalScore < -100) return -1.0f; // 白がかなり有利
                return 0.5f; // 引き分け扱い
            }

            // ゲーム終了時の結果を返す (1.0 = 黒勝利, -1.0 = 白勝利, 0.5 = 引分)
            return ReversiSimulator.GetResult(simState);
        }

        /// <summary>
        /// シミュレーション結果をこのノードから親ノードへ逆伝播させる
        /// </summary>
        public void Backpropagate(float result)
        {
            MCTSNode node = this;
            while (node != null)
            {
                node.VisitCount++;

                // MCTSのスコアは「現在のノードのプレイヤー」視点での勝利スコアである必要がある
                // シミュレーション結果は「黒」視点 (1.0 = 黒勝利)

                // ノードのプレイヤーが黒の場合
                if (node.State.CurrentPlayer == StoneColor.Black)
                {
                    if (result == 1.0f) node._winScore += 1.0;          // 黒の勝ち
                    else if (result == -1.0f) node._winScore += 0.0;    // 黒の負け
                    else if (result == 0.5f) node._winScore += 0.5;     // 引き分け
                }
                // ノードのプレイヤーが白の場合
                else if (node.State.CurrentPlayer == StoneColor.White)
                {
                    if (result == 1.0f) node._winScore += 0.0;          // 白の負け
                    else if (result == -1.0f) node._winScore += 1.0;    // 白の勝ち
                    else if (result == 0.5f) node._winScore += 0.5;     // 引き分け
                }

                node = node.Parent;
            }
        }

        public bool IsLeafNode() => Children.Count == 0;
        /// <summary>
        /// まだ試していない（展開していない）有効な手が1つでも残っているか
        /// </summary>
        public bool HasUntriedActions() => _untriedActions.Count > 0;

        /// <returns>子ノードがない場合は例外を返す</returns>
        public MCTSNode GetMostVisitedChild()
        {
            return Children.OrderByDescending(c => c.VisitCount).First();
        }
    }
}
