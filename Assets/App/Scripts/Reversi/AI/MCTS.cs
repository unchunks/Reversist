using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace App.Reversi.AI
{
    // ============================================================================
    // MCTSの検索結果を格納するクラス
    // ============================================================================
    public class MCTSSearchResult
    {
        public GameAction BestAction { get; }
        public int TotalSimulations { get; }
        public long ElapsedMilliseconds { get; }
        public double SimulationsPerSecond { get; }

        public MCTSSearchResult(GameAction bestAction, int totalSimulations, long elapsedMilliseconds)
        {
            BestAction = bestAction;
            TotalSimulations = totalSimulations;
            ElapsedMilliseconds = elapsedMilliseconds;
            SimulationsPerSecond = (elapsedMilliseconds > 0) ? (totalSimulations * 1000.0) / elapsedMilliseconds : 0;
        }
    }

    // ============================================================================
    // MCTS（モンテカルロ木探索）メインクラス
    // ============================================================================
    public class MCTS
    {
        private readonly Random _random;
        private const double EXPLORATION_CONSTANT = 1.41; // √2

        public MCTS()
        {
            _random = new Random();
        }

        /// <summary>
        /// MCTSで最善手を探索する
        /// </summary>
        public MCTSSearchResult Search(GameState initialState, int milliseconds)
        {
            UnityEngine.Debug.Log("[MCTS] Starting search for best move.");
            StoneColor aiColor = initialState.CurrentPlayer;
            Stopwatch sw = Stopwatch.StartNew();

            // 1. AIのゲーム中「最初の1手」かを判定
            bool isAIsFirstMove = false;
            if (aiColor == StoneColor.Black &&
                initialState.StoneCount[StoneColor.Black] + initialState.StoneCount[StoneColor.White] == 4)
            {
                // AIが先手（黒）で、初期状態（2対2）
                isAIsFirstMove = true;
            }
            else if (aiColor == StoneColor.White &&
                     initialState.StoneCount[StoneColor.Black] + initialState.StoneCount[StoneColor.White] == 5)
            {
                // AIが後手（白）で、相手が1手だけ打った状態
                isAIsFirstMove = true;
            }

            if (isAIsFirstMove)
            {
                UnityEngine.Debug.Log("[MCTS Override] Checking for Frozen on first move.");
                // 2. 有効な手をすべて取得
                var validActions = ReversiSimulator.GetValidActions(initialState);

                // 3. 有効な手の中に Frozen があるか探す
                GameAction frozenAction = validActions.FirstOrDefault(action => action.Type == StoneType.Frozen);

                if (frozenAction != null)
                {
                    // 4. あった場合、MCTS探索をスキップして即座に返す
                    sw.Stop();
                    UnityEngine.Debug.Log("[MCTS Override] Forcing first move: Frozen.");
                    return new MCTSSearchResult(frozenAction, 1, sw.ElapsedMilliseconds); // 1シミュレーションとして返す
                }
                // Frozen が使えない場合（在庫切れ、置ける場所がない等）、通常のMCTS探索にフォールスルー
            }

            MCTSNode rootNode = new MCTSNode(initialState, aiColor);

            // 合法手がない場合
            if (rootNode.GetUntriedActionsCount() == 0 && rootNode.GetChildrenCount() == 0)
            {
                sw.Stop();
                return new MCTSSearchResult(null, 0, sw.ElapsedMilliseconds);
            }

            int simulationCount = 0;

            // 時間制限まで探索を続ける
            while (sw.ElapsedMilliseconds < milliseconds)
            {
                // 1. Selection（選択）: UCB1値が最大の子ノードを選択
                MCTSNode selectedNode = Selection(rootNode);

                // 2. Expansion（展開）: 未試行の手があれば展開
                if (selectedNode.HasUntriedActions() && !selectedNode.IsTerminal())
                {
                    selectedNode = selectedNode.Expand(_random);
                }

                // 3. Simulation（シミュレーション）: ゲーム終了まで高速プレイアウト
                double result = Simulation(selectedNode, aiColor, _random);

                // 4. Backpropagation（逆伝播）: 結果を親ノードに伝播
                Backpropagation(selectedNode, result);

                simulationCount++;
            }

            sw.Stop();

            // 最も訪問回数が多い子ノードを選択
            MCTSNode bestChild = rootNode.GetMostVisitedChild();

            return new MCTSSearchResult(
                bestChild?.Action,
                simulationCount,
                sw.ElapsedMilliseconds
            );
        }

        /// <summary>
        /// Selection: 葉ノードまで選択を繰り返す
        /// </summary>
        private MCTSNode Selection(MCTSNode node)
        {
            while (!node.IsLeaf() && !node.IsTerminal())
            {
                node = node.SelectBestChildUCB1(EXPLORATION_CONSTANT);
            }
            return node;
        }

        /// <summary>
        /// Simulation: ゲーム終了まで高速プレイアウト
        /// </summary>
        private double Simulation(MCTSNode node, StoneColor aiColor, Random random)
        {
            // 既に終了している場合
            if (node.IsTerminal())
            {
                return EvaluateTerminalState(node.State, aiColor);
            }

            GameState simState = new GameState(node.State);
            int depth = 0;
            const int MAX_DEPTH = 50;

            // ゲーム終了または最大深度まで進める
            while (!simState.IsGameOver && depth < MAX_DEPTH)
            {
                var validActions = ReversiSimulator.GetValidActions(simState);

                if (validActions.Count == 0)
                {
                    // パス
                    simState.CurrentPlayer = simState.CurrentPlayer.Opponent();
                    var nextActions = ReversiSimulator.GetValidActions(simState);
                    if (nextActions.Count == 0)
                    {
                        simState.IsGameOver = true;
                        break;
                    }
                }
                else
                {
                    // 戦略的プレイアウト：評価値ベースの選択
                    GameAction selectedAction = SelectActionForSimulation(simState, validActions, aiColor, random);
                    simState = ReversiSimulator.ExecuteAction(simState, selectedAction);
                    depth++;
                }
            }

            return EvaluateState(simState, aiColor);
        }

        /// <summary>
        /// シミュレーション中の手選択（戦略的プレイアウト）
        /// </summary>
        private GameAction SelectActionForSimulation(GameState state, List<GameAction> actions, StoneColor aiColor, Random random)
        {
            double progress = CalculateProgress(state);
            bool isCurrentPlayerAI = (state.CurrentPlayer == aiColor);

            // 危険な手を除外
            var safeActions = FilterDangerousActions(state, actions, progress);
            if (safeActions.Count == 0)
            {
                safeActions = actions; // 全て危険ならそのまま
            }

            // 80%の確率で評価値ベース、20%でランダム（探索のバランス）
            if (random.NextDouble() < 0.8)
            {
                GameAction bestAction = null;
                double bestScore = isCurrentPlayerAI ? double.NegativeInfinity : double.PositiveInfinity;

                foreach (var action in safeActions)
                {
                    GameState nextState = ReversiSimulator.ExecuteAction(state, action);
                    double score = ReversiEvaluator.Evaluate(nextState);

                    // AI視点でスコアを調整
                    if (aiColor == StoneColor.White)
                    {
                        score = -score; // 白の場合は反転
                    }

                    bool isBetter = isCurrentPlayerAI ? (score > bestScore) : (score < bestScore);
                    if (isBetter)
                    {
                        bestScore = score;
                        bestAction = action;
                    }
                }

                return bestAction ?? safeActions[random.Next(safeActions.Count)];
            }
            else
            {
                return safeActions[random.Next(safeActions.Count)];
            }
        }

        /// <summary>
        /// 危険な手を除外する
        /// </summary>
        private List<GameAction> FilterDangerousActions(GameState state, List<GameAction> actions, double progress)
        {
            var safeActions = new List<GameAction>();
            bool hasDelayReverseInStack = state.DelayReverseStack.Count > 0;
            bool is10x10 = state.CurrentBoardSize == 10;

            foreach (var action in actions)
            {
                bool isDangerous = false;

                // 序盤（progress < 0.25）でのDelayReverse/Reverseは危険
                if (progress < 0.25 && (action.Type == StoneType.DelayReverse || action.Type == StoneType.Reverse))
                {
                    isDangerous = true;
                }

                // 既にDelayReverseがスタックにある場合、追加は危険
                if (hasDelayReverseInStack && action.Type == StoneType.DelayReverse)
                {
                    isDangerous = true;
                }

                // 10x10で相手がExtendを持っていない場合、先にExtendを使うのは不利
                if (is10x10 && action.Type == StoneType.Extend)
                {
                    StoneColor opponent = state.CurrentPlayer.Opponent();
                    bool opponentHasExtend = state.Inventories[opponent].AvailableCount[StoneType.Extend] > 0;
                    if (opponentHasExtend)
                    {
                        isDangerous = true;
                    }
                }

                if (!isDangerous)
                {
                    safeActions.Add(action);
                }
            }

            return safeActions;
        }

        /// <summary>
        /// Backpropagation: 結果を親に伝播
        /// </summary>
        private void Backpropagation(MCTSNode node, double result)
        {
            while (node != null)
            {
                node.Update(result);
                result = 1.0 - result; // 親は相手プレイヤー
                node = node.Parent;
            }
        }

        /// <summary>
        /// 終了状態の評価
        /// </summary>
        private double EvaluateTerminalState(GameState state, StoneColor aiColor)
        {
            int blackStones = state.StoneCount[StoneColor.Black];
            int whiteStones = state.StoneCount[StoneColor.White];

            bool aiWins = (aiColor == StoneColor.Black && blackStones > whiteStones) ||
                         (aiColor == StoneColor.White && whiteStones > blackStones);

            if (aiWins) return 1.0;
            if (blackStones == whiteStones) return 0.5;
            return 0.0;
        }

        /// <summary>
        /// 非終了状態の評価
        /// </summary>
        private double EvaluateState(GameState state, StoneColor aiColor)
        {
            if (state.IsGameOver)
            {
                return EvaluateTerminalState(state, aiColor);
            }

            double score = ReversiEvaluator.Evaluate(state);

            // AI視点でスコアを調整
            if (aiColor == StoneColor.White)
            {
                score = -score;
            }

            // スコアを0.0-1.0の範囲に正規化
            return 0.5 + (Math.Tanh(score / 500.0) * 0.5);
        }

        /// <summary>
        /// ゲーム進行度を計算
        /// </summary>
        private double CalculateProgress(GameState state)
        {
            int totalStones = state.StoneCount[StoneColor.Black] + state.StoneCount[StoneColor.White];
            int maxStones = state.CurrentBoardSize * state.CurrentBoardSize;
            return Math.Pow((double)totalStones / maxStones, 1.5);
        }
    }
}
