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

    /// <summary>
    /// MCTS（モンテカルロ木探索）メインクラス
    /// 修正版：石の性質（Reverse/DelayReverse）を考慮した厳格な自滅回避
    /// </summary>
    public class MCTS
    {
        private readonly Random _random;
        private const double EXPLORATION_CONSTANT = 1.414;

        // 安全性チェックの深さ
        private const int SAFETY_CHECK_DEPTH = 4;

        public MCTS()
        {
            _random = new Random();
        }

        public async UniTask<MCTSSearchResult> Search(GameState initialState, int milliseconds)
        {
            StoneColor aiColor = initialState.CurrentPlayer;
            Stopwatch sw = Stopwatch.StartNew();

            // 思考時間に応じてシミュレーションの深さを調整
            int playoutDepth;
            if (milliseconds < 1000) playoutDepth = 5;
            else if (milliseconds < 2000) playoutDepth = 20;
            else if (milliseconds < 3000) playoutDepth = 40;
            else playoutDepth = 64;

            long lastYieldTime = sw.ElapsedMilliseconds;
            long yieldInterval = 30;

            MCTSNode rootNode = new MCTSNode(initialState, aiColor);

            if (rootNode.GetUntriedActionsCount() == 0 && rootNode.GetChildrenCount() == 0)
            {
                sw.Stop();
                return new MCTSSearchResult(null, 0, sw.ElapsedMilliseconds);
            }

            int simulationCount = 0;
            long searchTimeLimit = (long)(milliseconds * 0.9);

            while (sw.ElapsedMilliseconds < searchTimeLimit)
            {
                if (sw.ElapsedMilliseconds - lastYieldTime > yieldInterval)
                {
                    await UniTask.Yield();
                    lastYieldTime = sw.ElapsedMilliseconds;
                    if (sw.ElapsedMilliseconds >= searchTimeLimit) break;
                }

                MCTSNode selectedNode = rootNode;
                while (!selectedNode.IsLeaf() && !selectedNode.IsTerminal())
                {
                    selectedNode = selectedNode.SelectBestChildUCB1(EXPLORATION_CONSTANT);
                }

                if (selectedNode.HasUntriedActions() && !selectedNode.IsTerminal())
                {
                    selectedNode = selectedNode.Expand(_random);
                }

                double result = Simulation(selectedNode, aiColor, playoutDepth);

                MCTSNode backNode = selectedNode;
                while (backNode != null)
                {
                    backNode.Update(result);
                    backNode = backNode.Parent;
                }

                simulationCount++;
            }

            sw.Stop();

            // 最終的な手の選択（厳格な安全性チェック）
            GameAction bestAction = SelectTrulySafeAction(rootNode, aiColor);

            return new MCTSSearchResult(
                bestAction,
                simulationCount,
                sw.ElapsedMilliseconds
            );
        }

        /// <summary>
        /// 安全性を最優先した行動選択
        /// </summary>
        private GameAction SelectTrulySafeAction(MCTSNode rootNode, StoneColor aiColor)
        {
            if (rootNode.Children == null || rootNode.Children.Count == 0) return null;

            // 1. まず、明らかに「自殺行為」である手を除外する
            // DelayReverseやReverseで自分の石が0または1になる手は、論外として候補から外す
            var validCandidates = new List<MCTSNode>();
            foreach (var child in rootNode.Children)
            {
                if (!IsSuicidalMove(child.State, aiColor))
                {
                    validCandidates.Add(child);
                }
            }

            // もしMCTSの候補がすべて自殺手だった場合（または候補がない場合）、
            // 全ての合法手から「死なない手」を再検索する（緊急回避）
            if (validCandidates.Count == 0)
            {
                var emergencyAction = FindAnySurvivalAction(rootNode.State, aiColor);
                if (emergencyAction != null)
                {
                    UnityEngine.Debug.LogWarning("[AI Safety] Emergency survival move selected.");
                    return emergencyAction;
                }
                // どうあがいても死ぬなら、訪問回数最大の手（MCTSの推奨）を返す
                return rootNode.GetMostVisitedChild().Action;
            }

            // 2. 生き残る候補の中で、MCTSの訪問回数順にソート
            var sortedCandidates = validCandidates.OrderByDescending(c => c.VisitCount).ToList();

            // 3. 上位候補に対して、深い安全性チェック（相手の妙手で死なないか）を行う
            foreach (var child in sortedCandidates)
            {
                // 既に自殺チェックは通過しているので、ここでは「相手の次の手」以降を深く読む
                // 石数チェック用の基準値（現状維持できているか）
                int currentStoneCount = rootNode.State.StoneCount[aiColor];

                if (IsStateSafeDeep(child.State, aiColor, SAFETY_CHECK_DEPTH, currentStoneCount))
                {
                    return child.Action;
                }
            }

            // 4. 安全な手が見つからない場合
            // 「角を取られない」手の中で、最も石数が多い（生存確率が高い）手を選ぶ
            var bestSurvival = sortedCandidates
                .OrderByDescending(c => SurvivalScore(c, aiColor))
                .First();

            return bestSurvival.Action;
        }

        /// <summary>
        /// その手が「自殺行為」かどうかを判定する（浅いチェック）
        /// </summary>
        private bool IsSuicidalMove(GameState nextState, StoneColor aiColor)
        {
            // 1. 打った直後に自分の石が0個（即死）
            // DelayReverseを打った場合、相手の色として置くため、ここで0になりやすい
            if (nextState.StoneCount[aiColor] == 0) return true;

            // 2. 打った直後に自分の石が1個（瀕死）
            // Reverseを打ったが、反転後に1個しか残らない場合など。
            // 次の相手のターンでその1個を取られたら全滅（Wipeout）するので、ほぼ負け確定。
            // ただし、打った時点でゲーム終了（自分が勝ち）ならOK
            if (!nextState.IsGameOver && nextState.StoneCount[aiColor] <= 1) return true;

            return false;
        }

        /// <summary>
        /// 緊急回避：とにかく死なない手を探す
        /// </summary>
        private GameAction FindAnySurvivalAction(GameState currentState, StoneColor aiColor)
        {
            var allActions = ReversiSimulator.GetValidActions(currentState);
            // Normal石を優先、特殊石は後回し（特殊石は事故りやすい）
            var sortedActions = allActions.OrderBy(a => a.Type == StoneType.Normal ? 0 : 1).ToList();

            foreach (var action in sortedActions)
            {
                GameState nextState = ReversiSimulator.ExecuteAction(currentState, action);
                if (!IsSuicidalMove(nextState, aiColor))
                {
                    // さらに、次の相手の一手で全滅させられないか簡易チェック
                    if (CanSurviveNextTurn(nextState, aiColor))
                    {
                        return action;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 相手の次の1手で全滅しないかチェック
        /// </summary>
        private bool CanSurviveNextTurn(GameState state, StoneColor aiColor)
        {
            if (state.IsGameOver) return true;
            var opponentActions = ReversiSimulator.GetValidActions(state);
            if (opponentActions.Count == 0) return true;

            foreach (var oppAction in opponentActions)
            {
                // 相手が角を取れるなら危険とみなす（生存とは別だが、勝ち筋として）
                if (IsCorner(oppAction.Position, state.CurrentBoardSize)) return false;

                GameState afterState = ReversiSimulator.ExecuteAction(state, oppAction);
                if (afterState.StoneCount[aiColor] == 0) return false; // 全滅させられる
            }
            return true;
        }

        private double SurvivalScore(MCTSNode node, StoneColor aiColor)
        {
            double score = node.VisitCount; // 基本は訪問回数

            // 石が多いほうが生存しやすい
            score += node.State.StoneCount[aiColor] * 100.0;

            // DelayReverseはリスクとして減点
            if (node.Action.Type == StoneType.DelayReverse) score -= 5000.0;

            return score;
        }

        /// <summary>
        /// 即死につながるかを深くチェック（枝刈り最適化版）
        /// </summary>
        private bool IsStateSafeDeep(GameState state, StoneColor myColor, int depth, int baselineCount)
        {
            // 基本的な敗北チェック
            if (state.StoneCount[myColor] == 0) return false;
            if (state.IsGameOver) return state.StoneCount[myColor] > 0; // 勝ってるか引き分けならOK
            if (depth <= 0) return true;

            var actions = ReversiSimulator.GetValidActions(state);

            // パスの場合
            if (actions.Count == 0)
            {
                GameState nextState = new GameState(state);
                nextState.CurrentPlayer = nextState.CurrentPlayer.Opponent();
                if (ReversiSimulator.GetValidActions(nextState).Count == 0)
                {
                    nextState.IsGameOver = true;
                    return nextState.StoneCount[myColor] > 0;
                }
                return IsStateSafeDeep(nextState, myColor, depth - 1, baselineCount);
            }

            if (state.CurrentPlayer == myColor)
            {
                // 自分のターン：1つでも安全な手を選べればOK
                foreach (var action in actions)
                {
                    GameState nextState = ReversiSimulator.ExecuteAction(state, action);
                    // 自殺手は選ばない前提
                    if (IsSuicidalMove(nextState, myColor)) continue;

                    if (IsStateSafeDeep(nextState, myColor, depth - 1, baselineCount)) return true;
                }
                return false; // どの手を選んでも死ぬ（詰み）
            }
            else
            {
                // 相手のターン：相手が「最善手（私を殺す手）」を打ってきても耐えられるか？
                foreach (var action in actions)
                {
                    // 1. 相手が角を取る手 -> 危険とみなす
                    if (IsCorner(action.Position, state.CurrentBoardSize)) return false;

                    GameState nextState = ReversiSimulator.ExecuteAction(state, action);

                    // 2. 相手の手で全滅させられる -> NG
                    if (nextState.StoneCount[myColor] == 0) return false;

                    // 3. 再帰チェック
                    if (!IsStateSafeDeep(nextState, myColor, depth - 1, baselineCount)) return false;
                }
                return true; // どの一手を打たれても耐えられる
            }
        }

        private double Simulation(MCTSNode node, StoneColor aiColor, int maxDepth)
        {
            if (node.IsTerminal()) return GetTerminalScore(node.State, aiColor);

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
                    // 相手を全滅させる手があれば優先、なければ角、なければランダム
                    GameAction selectedAction = SelectKillerMove(simState, actions);
                    simState = ReversiSimulator.ExecuteAction(simState, selectedAction);
                    moves++;
                }
            }

            if (simState.IsGameOver) return GetTerminalScore(simState, aiColor);
            return EvaluateNonTerminalState(simState, aiColor);
        }

        private GameAction SelectKillerMove(GameState state, List<GameAction> actions)
        {
            StoneColor currentPlayer = state.CurrentPlayer;
            StoneColor opponent = currentPlayer.Opponent();
            int boardSize = state.CurrentBoardSize;

            // 1. 相手を全滅させる手（即勝ち）
            foreach (var action in actions)
            {
                GameState next = ReversiSimulator.ExecuteAction(state, action);
                if (next.StoneCount[opponent] == 0) return action;
            }

            // 2. 角を取る手
            foreach (var action in actions)
            {
                if (IsCorner(action.Position, boardSize)) return action;
            }

            // 3. ランダム（ただしDelayReverseは避ける）
            // DelayReverseはシミュレーション（ランダム打ち）においては自滅率が高すぎるため、
            // プレイアウト中は確率的に選ばれないようにする
            var safeActions = actions.Where(a => a.Type != StoneType.DelayReverse).ToList();
            if (safeActions.Count > 0)
            {
                return safeActions[_random.Next(safeActions.Count)];
            }

            return actions[_random.Next(actions.Count)];
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
            if (state.StoneCount[aiColor] == 0) return 0.0;
            if (state.StoneCount[aiColor.Opponent()] == 0) return 1.0;

            double score = ReversiEvaluator.Evaluate(state);
            if (aiColor == StoneColor.White) score = -score;
            return 0.5 + 0.5 * Math.Tanh(score / 200.0);
        }
    }
}
