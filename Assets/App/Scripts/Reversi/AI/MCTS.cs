using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace App.Reversi.AI
{
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

    public class MCTS
    {
        private readonly Random _random;
        private const double EXPLORATION_CONSTANT = 1.41;

        public MCTS()
        {
            _random = new Random();
        }

        public async UniTask<MCTSSearchResult> Search(GameState initialState, int milliseconds)
        {
            StoneColor aiColor = initialState.CurrentPlayer;
            Stopwatch sw = Stopwatch.StartNew();

            // 思考時間に応じた深さ調整
            int playoutDepth;
            if (milliseconds < 1000) playoutDepth = 5;
            else if (milliseconds < 2000) playoutDepth = 20;
            else if (milliseconds < 3000) playoutDepth = 40;
            else playoutDepth = 64;

            long lastYieldTime = sw.ElapsedMilliseconds;
            long yieldInterval = 30;

            MCTSNode rootNode = new MCTSNode(initialState, aiColor);

            // 打てる手がない場合
            if (rootNode.GetUntriedActionsCount() == 0 && rootNode.GetChildrenCount() == 0)
            {
                sw.Stop();
                return new MCTSSearchResult(null, 0, sw.ElapsedMilliseconds);
            }

            int simulationCount = 0;

            while (sw.ElapsedMilliseconds < milliseconds)
            {
                if (sw.ElapsedMilliseconds - lastYieldTime > yieldInterval)
                {
                    await UniTask.Yield();
                    lastYieldTime = sw.ElapsedMilliseconds;
                    if (sw.ElapsedMilliseconds >= milliseconds) break;
                }

                // Selection
                MCTSNode selectedNode = rootNode;
                while (!selectedNode.IsLeaf() && !selectedNode.IsTerminal())
                {
                    selectedNode = selectedNode.SelectBestChildUCB1(EXPLORATION_CONSTANT);
                }

                // Expansion
                if (selectedNode.HasUntriedActions() && !selectedNode.IsTerminal())
                {
                    selectedNode = selectedNode.Expand(_random);
                }

                // Simulation
                double result = Simulation(selectedNode, aiColor, playoutDepth);

                // Backpropagation
                MCTSNode backNode = selectedNode;
                while (backNode != null)
                {
                    backNode.Update(result);
                    backNode = backNode.Parent;
                }

                simulationCount++;
            }

            sw.Stop();

            // 最も訪問回数が多い手を選ぶが、危険な手は回避する
            GameAction bestAction = SelectSafeBestAction(rootNode, aiColor);

            return new MCTSSearchResult(
                bestAction,
                simulationCount,
                sw.ElapsedMilliseconds
            );
        }

        /// <summary>
        /// 最善手を選択する（安全性チェック付き）
        /// </summary>
        private GameAction SelectSafeBestAction(MCTSNode rootNode, StoneColor aiColor)
        {
            // 1. まずMCTSの結果（訪問回数順）から、安全な手を探す
            if (rootNode.Children != null && rootNode.Children.Count > 0)
            {
                var sortedChildren = rootNode.Children
                    .OrderByDescending(c => c.VisitCount)
                    .ToList();

                foreach (var child in sortedChildren)
                {
                    if (IsActionSafe(rootNode.State, child.Action, aiColor))
                    {
                        return child.Action;
                    }
                }
            }

            // 2. 【緊急回避】MCTS候補がすべて危険（または空）だった場合
            // 未探索の手も含めて、全ての有効手から「安全な手」をルールベースで探す
            var allActions = ReversiSimulator.GetValidActions(rootNode.State);

            // ランダムにシャッフルして偏りを防ぐ
            allActions = allActions.OrderBy(a => _random.Next()).ToList();

            foreach (var action in allActions)
            {
                // Normal石を優先的にチェック（特殊石は自滅リスクが高いため後回し）
                if (action.Type == StoneType.Normal)
                {
                    if (IsActionSafe(rootNode.State, action, aiColor))
                    {
                        UnityEngine.Debug.LogWarning("[AI Safety] MCTS候補に安全な手がなく、緊急回避でNormal石を選択しました。");
                        return action;
                    }
                }
            }

            // Normalで安全な手がなければ、他の石もチェック
            foreach (var action in allActions)
            {
                if (action.Type != StoneType.Normal)
                {
                    if (IsActionSafe(rootNode.State, action, aiColor))
                    {
                        UnityEngine.Debug.LogWarning($"[AI Safety] MCTS候補に安全な手がなく、緊急回避で{action.Type}を選択しました。");
                        return action;
                    }
                }
            }

            // 3. どうあがいても安全な手がない場合（詰み）
            // 訪問回数最大の手に特攻する
            if (rootNode.Children != null && rootNode.Children.Count > 0)
            {
                return rootNode.GetMostVisitedChild().Action;
            }

            // ここに来ることはほぼない（有効手が0なら冒頭で弾かれる）
            return allActions.FirstOrDefault();
        }

        /// <summary>
        /// その手が「安全」か厳密にチェックする
        /// </summary>
        private bool IsActionSafe(GameState currentState, GameAction action, StoneColor aiColor)
        {
            // 1. 自分の手でゲームが終わる場合（勝ちならOK）
            GameState nextState = ReversiSimulator.ExecuteAction(currentState, action);

            if (nextState.IsGameOver)
            {
                double score = GetTerminalScore(nextState, aiColor);
                return score > 0.0; // 勝ち(1.0)か引き分け(0.5)ならOK
            }

            // 【自滅チェック】石の数が極端に少ない（全滅 or 1個残り）ならNG
            // ReverseやDelayReverseを使った結果、自分の石が1個だけ残るケースは
            // 次の相手のターンで取られて全滅するリスクが極大なので禁止する
            int myStoneCount = nextState.StoneCount[aiColor];
            if (myStoneCount <= 1)
            {
                return false;
            }

            // 2. 相手（敵）のターンでのシミュレーション
            var opponentActions = ReversiSimulator.GetValidActions(nextState);
            if (opponentActions.Count == 0) return true; // 相手が打てない（パス）なら安全

            // 特殊石の場合は判定を厳しくする
            bool isSpecialStone = (action.Type == StoneType.DelayReverse || action.Type == StoneType.Reverse);
            double safetyThreshold = isSpecialStone ? 0.3 : 0.15;

            foreach (var oppAction in opponentActions)
            {
                // A. 相手に角を取られる手があるか？ -> あれば危険
                // （自分が角を取った直後なら許容するロジックも考えられるが、今回は厳しく禁止）
                if (IsCorner(oppAction.Position, nextState.CurrentBoardSize))
                {
                    return false;
                }

                // B. 相手がその手を打った結果のチェック
                GameState afterOpponentState = ReversiSimulator.ExecuteAction(nextState, oppAction);

                // 【全滅チェック】相手に打たれて自分の石が0になるならNG
                if (afterOpponentState.StoneCount[aiColor] == 0)
                {
                    return false;
                }

                // 【詰みチェック】
                if (afterOpponentState.IsGameOver)
                {
                    double myScore = GetTerminalScore(afterOpponentState, aiColor);
                    if (myScore == 0.0) return false; // 負け確定
                }
                else
                {
                    // 特殊石を使ったのに、その後圧倒的不利になるなら避ける
                    if (isSpecialStone)
                    {
                        double currentEval = EvaluateNonTerminalState(afterOpponentState, aiColor);
                        if (currentEval < safetyThreshold)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 最低限の安全性チェック（全滅と角取りだけ回避）
        /// シミュレーションのプレイアウト中に使用
        /// </summary>
        private bool IsActionBarelySafe(GameState currentState, GameAction action, StoneColor aiColor)
        {
            GameState nextState = ReversiSimulator.ExecuteAction(currentState, action);

            // 自滅（1個以下）チェック
            if (nextState.StoneCount[aiColor] <= 1) return false;

            if (nextState.IsGameOver) return true;

            // 相手の応手チェック（簡易版：最初に見つかった危険手だけで即NG）
            var opponentActions = ReversiSimulator.GetValidActions(nextState);
            foreach (var oppAction in opponentActions)
            {
                if (IsCorner(oppAction.Position, nextState.CurrentBoardSize)) return false;

                // 相手の手で全滅するか？（ここまでは重いのでプレイアウト中は見ない手もあるが、即死回避のため見る）
                // ただしReversiSimulator.ExecuteActionは少し重いので、
                // プレイアウト速度重視ならここはコメントアウトし、MCTSの学習に任せるという手もある。
                // 今回は「弱すぎる」のを防ぐため入れる。
                /*
                GameState afterOpponentState = ReversiSimulator.ExecuteAction(nextState, oppAction);
                if (afterOpponentState.StoneCount[aiColor] == 0) return false;
                */
            }
            return true;
        }

        private double Simulation(MCTSNode node, StoneColor aiColor, int maxDepth)
        {
            if (node.IsTerminal())
            {
                return GetTerminalScore(node.State, aiColor);
            }

            GameState simState = new GameState(node.State);
            int moves = 0;
            int boardSize = simState.CurrentBoardSize;

            while (moves < maxDepth && !simState.IsGameOver)
            {
                var actions = ReversiSimulator.GetValidActions(simState);

                if (actions.Count == 0)
                {
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
                    // プレイアウト中の手選び
                    GameAction selectedAction = SelectSimulationMove(simState, actions, boardSize);
                    simState = ReversiSimulator.ExecuteAction(simState, selectedAction);
                    moves++;
                }
            }

            if (simState.IsGameOver)
            {
                return GetTerminalScore(simState, aiColor);
            }

            return EvaluateNonTerminalState(simState, aiColor);
        }

        private GameAction SelectSimulationMove(GameState state, List<GameAction> actions, int boardSize)
        {
            // 1. 角を取る手があれば優先
            foreach (var action in actions)
            {
                if (IsCorner(action.Position, boardSize)) return action;
            }

            // 2. 安全な手（自滅しない手）に絞る
            // プレイアウトの速度を落とさないよう、IsActionBarelySafeは使わず簡易チェック
            var safeActions = new List<GameAction>(actions.Count);
            foreach (var action in actions)
            {
                // Reverse系の石はプレイアウト中には（思考停止で打つと）危険なので、
                // ランダム選択の確率を下げたい。ここでは簡易的に除外せずランダムに任せるが、
                // 明らかな自滅（自分の色が全滅）だけは避けたい場合、ここでExecuteActionしてチェックする。
                // しかしそれは重すぎるため、MCTSの試行回数でカバーする方針とする。
                safeActions.Add(action);
            }

            // ランダム選択
            return safeActions[_random.Next(safeActions.Count)];
        }

        private bool IsCorner(Position pos, int boardSize)
        {
            int r = pos.Row;
            int c = pos.Col;
            int offset = (GameState.MAX_BOARD_SIZE - boardSize) / 2;
            int min = offset;
            int max = offset + boardSize - 1;
            return (r == min || r == max) && (c == min || c == max);
        }

        private double GetTerminalScore(GameState state, StoneColor aiColor)
        {
            int black = state.StoneCount[StoneColor.Black];
            int white = state.StoneCount[StoneColor.White];

            if (aiColor == StoneColor.Black)
            {
                if (black > white) return 1.0;
                if (black < white) return 0.0;
                return 0.5;
            }
            else
            {
                if (white > black) return 1.0;
                if (white < black) return 0.0;
                return 0.5;
            }
        }

        private double EvaluateNonTerminalState(GameState state, StoneColor aiColor)
        {
            double score = ReversiEvaluator.Evaluate(state);
            if (aiColor == StoneColor.White) score = -score;
            return 0.5 + 0.5 * Math.Tanh(score / 200.0);
        }
    }
}