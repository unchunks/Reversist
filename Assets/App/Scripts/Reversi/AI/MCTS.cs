using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace App.Reversi.AI
{
    /// <summary>
    /// AIの思考詳細データ（可視化用）
    /// </summary>
    public class AIEvaluationData
    {
        public Position Position;      // 着手位置
        public StoneType Type;         // 石の種類
        public int VisitCount;         // MCTSでのシミュレーション回数
        public double WinRate;         // 勝率（評価値）
        public bool IsBest;            // 最善手かどうか
    }

    public class MCTSSearchResult
    {
        public GameAction BestAction { get; }
        public int TotalSimulations { get; }
        public long ElapsedMilliseconds { get; }
        public double SimulationsPerSecond { get; }
        public List<AIEvaluationData> Evaluations { get; }

        public MCTSSearchResult(GameAction bestAction, int totalSimulations, long elapsedMilliseconds, List<AIEvaluationData> evaluations = null)
        {
            BestAction = bestAction;
            TotalSimulations = totalSimulations;
            ElapsedMilliseconds = elapsedMilliseconds;
            SimulationsPerSecond = (elapsedMilliseconds > 0) ? (totalSimulations * 1000.0) / elapsedMilliseconds : 0;
            Evaluations = evaluations;
        }
    }

    /// <summary>
    /// MCTS（モンテカルロ木探索）メインクラス
    /// </summary>
    public class MCTS
    {
        private readonly Random _random;
        private const double EXPLORATION_CONSTANT = 1.414;

        // 安全性チェックの深さ（この深さまでは全探索に近いチェックを行う）
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
            else playoutDepth = 64; // ほぼ最後まで

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
            // 最後の選択処理のために少し時間を残す
            long searchTimeLimit = (long)(milliseconds * 0.9);

            while (sw.ElapsedMilliseconds < searchTimeLimit)
            {
                if (sw.ElapsedMilliseconds - lastYieldTime > yieldInterval)
                {
                    await UniTask.Yield();
                    lastYieldTime = sw.ElapsedMilliseconds;
                    if (sw.ElapsedMilliseconds >= searchTimeLimit) break;
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

                // Simulation (PlayOut)
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

            // 最終的な手の選択（すぐに負ける手でないか、安全性チェック）
            GameAction bestAction = SelectTrulySafeAction(rootNode, aiColor);

            // 可視化用データの作成
            var evaluations = new List<AIEvaluationData>();
            if (rootNode.Children != null)
            {
                foreach (var child in rootNode.Children)
                {
                    evaluations.Add(new AIEvaluationData
                    {
                        Position = child.Action.Position,
                        Type = child.Action.Type,
                        VisitCount = child.VisitCount,
                        WinRate = child.VisitCount > 0 ? child.TotalScore / child.VisitCount : 0.0,
                        IsBest = (child.Action == bestAction)
                    });
                }
            }

            return new MCTSSearchResult(
                bestAction,
                simulationCount,
                sw.ElapsedMilliseconds,
                evaluations
            );
        }

        /// <summary>
        /// 安全性を最優先した行動選択
        /// MCTSの統計結果だけでなく、ルールベースで危険な手を弾く
        /// </summary>
        private GameAction SelectTrulySafeAction(MCTSNode rootNode, StoneColor aiColor)
        {
            if (rootNode.Children == null || rootNode.Children.Count == 0) return null;

            // 明らかに負けが確定する手を除外する
            var validCandidates = new List<MCTSNode>();
            foreach (var child in rootNode.Children)
            {
                if (!IsSuicidalMove(child.State, aiColor))
                {
                    validCandidates.Add(child);
                }
            }

            // もしMCTSの候補がすべて負け確定の手だった場合（または候補がない場合）、
            // 未探索の手も含めた全ての合法手から「負けない手」を再検索する（緊急回避）
            if (validCandidates.Count == 0)
            {
                var emergencyAction = FindAnySurvivalAction(rootNode.State, aiColor);
                if (emergencyAction != null)
                {
                    UnityEngine.Debug.LogWarning("[AI Safety] Emergency survival move selected.");
                    return emergencyAction;
                }
                // どうあがいても負けなら、訪問回数最大の手を返す
                return rootNode.GetMostVisitedChild().Action;
            }

            // 生き残る候補の中で、MCTSの訪問回数順にソート
            var sortedCandidates = validCandidates.OrderByDescending(c => c.VisitCount).ToList();

            // 上位候補に対して、深い安全性チェック（相手の妙手で負けないか）を行う
            int currentStoneCount = rootNode.State.StoneCount[aiColor];

            foreach (var child in sortedCandidates)
            {
                // 安全性チェック
                if (IsStateSafeDeep(child.State, aiColor, SAFETY_CHECK_DEPTH, currentStoneCount))
                {
                    return child.Action;
                }
            }

            // 安全な手が見つからない場合
            // 「角を取られない」手の中で、最も石数が多い（生存確率が高い）手を選ぶ
            // 訪問回数だけで選ぶと、AIが楽観視して「負けやすい手」を選んでしまうため
            var bestSurvival = sortedCandidates
                .OrderByDescending(c => SurvivalScore(c, aiColor))
                .First();

            return bestSurvival.Action;
        }

        /// <summary>
        /// その手が負けが確定する手かどうかを判定する（浅いチェック）
        /// </summary>
        private bool IsSuicidalMove(GameState nextState, StoneColor aiColor)
        {
            // 打った直後に自分の石が0個（即死）
            // DelayReverseを打った場合、相手の色として置くため、ここで0になりやすい
            if (nextState.StoneCount[aiColor] == 0) return true;

            // 打った直後に自分の石が1個（瀕死）
            // Reverseを打ったが、反転後に1個しか残らない場合など。
            // 次の相手のターンでその1個を取られたら全滅（Wipeout）するので、ほぼ負け確定。
            // ただし、打った時点でゲーム終了（自分が勝ち）ならOK（のちにCollapseなどを追加したとき用）
            if (!nextState.IsGameOver && nextState.StoneCount[aiColor] <= 1) return true;

            return false;
        }

        /// <summary>
        /// 緊急回避：とにかく負けない手を探す
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
                // 相手が角を取れるなら危険（勝負としては負け筋だが、ここでは全滅回避を優先してチェックしない手もあるが、やはり危険）
                if (IsCorner(oppAction.Position, state.CurrentBoardSize)) return false;

                GameState afterState = ReversiSimulator.ExecuteAction(state, oppAction);
                if (afterState.StoneCount[aiColor] == 0) return false; // 全滅させられる
            }
            return true;
        }

        private double SurvivalScore(MCTSNode node, StoneColor aiColor)
        {
            double score = node.VisitCount; // 基本は訪問回数

            // 石が多いほうが生存しやすい（即死回避）
            score += node.State.StoneCount[aiColor] * 50.0;

            // DelayReverseはリスクとして大幅減点
            if (node.Action.Type == StoneType.DelayReverse) score -= 5000.0;

            // Reverseも枚数が減るリスクがあるので少し減点
            if (node.Action.Type == StoneType.Reverse) score -= 1000.0;

            return score;
        }

        /// <summary>
        /// 即死につながるかを深くチェック（再帰探索）
        /// </summary>
        private bool IsStateSafeDeep(GameState state, StoneColor myColor, int depth, int baselineCount)
        {
            // 基本的な敗北チェック
            if (state.StoneCount[myColor] == 0) return false;

            // 激減チェック（石数が極端に減るのは危険兆候）
            if (baselineCount > 6 && state.StoneCount[myColor] < baselineCount * 0.5) return false;

            if (state.IsGameOver) return state.StoneCount[myColor] > 0; // 勝ちか引き分けならOK
            if (depth <= 0) return true;

            var actions = ReversiSimulator.GetValidActions(state);

            // パスの場合
            if (actions.Count == 0)
            {
                GameState nextState = new GameState(state);
                nextState.CurrentPlayer = nextState.CurrentPlayer.Opponent();

                // 相手も打てないなら終了
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

                    // 前提として、明らかに負けが確定する手は選ばない
                    if (IsSuicidalMove(nextState, myColor)) continue;

                    if (IsStateSafeDeep(nextState, myColor, depth - 1, baselineCount)) return true;
                }
                return false; // 詰み
            }
            else
            {
                // 相手のターン：相手が「最善手（AIを倒す手）」を打ってきても耐えられるか？
                foreach (var action in actions)
                {
                    // 相手が角を取る手 -> 非常に危険
                    if (IsCorner(action.Position, state.CurrentBoardSize)) return false;

                    GameState nextState = ReversiSimulator.ExecuteAction(state, action);

                    // 相手の手で全滅させられる -> NG
                    if (nextState.StoneCount[myColor] == 0) return false;

                    // 再帰チェック
                    if (!IsStateSafeDeep(nextState, myColor, depth - 1, baselineCount)) return false;
                }
                return true; // どの一手を打たれても耐えられる
            }
        }

        // --- シミュレーション（プレイアウト）関連 ---

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
                    // キラームーブ（相手を全滅させる手）や角取りを優先
                    GameAction selectedAction = SelectSimulationAction(simState, actions);
                    simState = ReversiSimulator.ExecuteAction(simState, selectedAction);
                    moves++;
                }
            }

            if (simState.IsGameOver) return GetTerminalScore(simState, aiColor);
            return EvaluateNonTerminalState(simState, aiColor);
        }

        /// <summary>
        /// プレイアウト中の手選びロジック
        /// </summary>
        private GameAction SelectSimulationAction(GameState state, List<GameAction> actions)
        {
            StoneColor currentPlayer = state.CurrentPlayer;
            StoneColor opponent = currentPlayer.Opponent();
            int boardSize = state.CurrentBoardSize;

            // 相手を全滅させる手（即勝ち）があれば最優先
            foreach (var action in actions)
            {
                // 結果を予測（ExecuteActionは重いが、勝敗直結なのでチェックする価値あり）
                GameState next = ReversiSimulator.ExecuteAction(state, action);
                if (next.StoneCount[opponent] == 0) return action;
            }

            // 角を取る手があれば優先
            foreach (var action in actions)
            {
                if (IsCorner(action.Position, boardSize)) return action;
            }

            // 重み付きランダム選択
            return SelectWeightedRandomMove(actions, boardSize);
        }

        private GameAction SelectWeightedRandomMove(List<GameAction> actions, int boardSize)
        {
            // 稀に完全ランダム（探索の多様性確保）
            if (_random.NextDouble() < 0.1) return actions[_random.Next(actions.Count)];

            GameAction bestAction = null;
            double bestScore = double.MinValue;

            foreach (var action in actions)
            {
                double score = 0;
                // 角はすでにチェック済みだが念のため
                if (IsCorner(action.Position, boardSize)) score += 50.0;

                // DelayReverseはプレイアウト中のランダム打ちでは自爆率が高いため避ける
                if (action.Type == StoneType.DelayReverse) score -= 30.0;

                // Reverseもやや避ける
                if (action.Type == StoneType.Reverse) score -= 10.0;

                if (action.Type == StoneType.Extend) score += 10.0;

                // ランダム性
                score += _random.NextDouble() * 10.0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = action;
                }
            }
            return bestAction ?? actions[0];
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

            // 全滅判定
            StoneColor opponent = aiColor.Opponent();
            if (state.StoneCount[aiColor] == 0) return 0.0; // 自分が全滅＝負け
            if (state.StoneCount[opponent] == 0) return 1.0; // 相手が全滅＝勝ち

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
            // 生存チェック
            if (state.StoneCount[aiColor] == 0) return 0.0;
            if (state.StoneCount[aiColor.Opponent()] == 0) return 1.0;

            double score = ReversiEvaluator.Evaluate(state);
            if (aiColor == StoneColor.White) score = -score;
            return 0.5 + 0.5 * Math.Tanh(score / 200.0);
        }
    }
}
