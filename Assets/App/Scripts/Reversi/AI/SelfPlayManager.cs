using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace App.Reversi.AI
{
    /// <summary>
    /// AIの自己対戦を実行し、教師データ（棋譜）を生成する
    /// </summary>
    public class SelfPlayManager : MonoBehaviour
    {
        [Header("学習設定")]
        [SerializeField] private int _gamesToPlay = 100; // 生成するゲーム数
        [SerializeField] private int _mctsIterations = 100; // 1手あたりのMCTS探索回数
        [SerializeField] private string _outputDirectory = "TrainingData";

        [ContextMenu("Start Self Play")]
        public async void StartSelfPlay()
        {
            string dirPath = Path.Combine(Application.dataPath, _outputDirectory);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            Debug.Log($"自己対戦を開始します。総ゲーム数: {_gamesToPlay}");

            // 重い自己対戦を別スレッドで実行
            await UniTask.RunOnThreadPool(() =>
            {
                for (int i = 0; i < _gamesToPlay; i++)
                {
                    RunSingleGame(i, dirPath);
                }
            });

            // forループはメインスレッドで実行する
            for (int i = 0; i < _gamesToPlay; i++)
            {
                int gameIndex = i; // ラムダ式でキャプチャするためにローカル変数にコピー

                // RunSingleGameの実行「だけ」を別スレッド（ThreadPool）に任せる
                await UniTask.RunOnThreadPool(() =>
                {
                    RunSingleGame(gameIndex, dirPath);
                });

                // 1ゲーム終わるごとにメインスレッドに戻ってくる
                // ここで進捗ログを1回だけ出力する
                Debug.Log($"進捗: {i + 1} / {_gamesToPlay} ゲーム完了");
            }

            Debug.Log($"自己対戦が完了しました。 {_gamesToPlay} ゲームの棋譜を生成。");

        }

        /// <summary>
        /// 1ゲーム分の自己対戦を実行し、棋譜を保存する
        /// </summary>
        private void RunSingleGame(int gameIndex, string dirPath)
        {
            GameState state = new GameState();
            var gameRecord = new GameRecord();

            // MCTS探索結果（Policy）を一時的に保持するリスト
            var mctsPolicies = new List<(GameState state, MCTSNode root)>();

            while (!state.IsGameOver)
            {
                // MCTSで最善の手を探索
                MCTSNode rootNode = new MCTSNode(state);
                GameAction bestAction = Search(rootNode, _mctsIterations); // 時間ではなく回数で指定

                // 棋譜（盤面とMCTSの探索結果）を記録
                mctsPolicies.Add((new GameState(state), rootNode));

                // 手を実行
                state = ReversiSimulator.ExecuteAction(state, bestAction);
            }

            // --- ゲーム終了 ---
            float finalResult = ReversiSimulator.GetResult(state); // 最終的な勝敗

            // 棋譜データ(GameRecord)を構築
            foreach (var (gameState, root) in mctsPolicies)
            {
                var sample = new TrainingSample();

                // 入力テンソル (GameState -> float[])
                sample.inputTensor = ConvertStateToInputTensor(gameState);

                // Policy (MCTSの訪問回数 -> float[])
                sample.policy = ConvertPolicyToTensor(root);

                // Value (最終的な勝敗)
                // AI(黒)視点の勝敗に変換
                if (gameState.CurrentPlayer == StoneColor.Black)
                {
                    if (finalResult == 1.0f) sample.value = 1.0f;
                    else if (finalResult == -1.0f) sample.value = -1.0f;
                    else sample.value = 0.0f;
                }
                else // 相手(白)視点
                {
                    if (finalResult == 1.0f) sample.value = -1.0f; // 黒が勝った＝白の負け
                    else if (finalResult == -1.0f) sample.value = 1.0f; // 黒が負けた＝白の勝ち
                    else sample.value = 0.0f;
                }

                gameRecord.samples.Add(sample);
            }

            // ファイルに書き出し
            string filePath = Path.Combine(dirPath, $"game_{gameIndex:D5}.json");
            TrainingDataWriter.Write(gameRecord, filePath);
        }

        // MCTS.Searchを回数ベースに変更したもの
        private GameAction Search(MCTSNode rootNode, int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                MCTSNode node = rootNode;
                while (!node.HasUntriedActions() && node.State.IsGameOver)
                {
                    node = node.SelectBestChild();
                }
                if (node.HasUntriedActions())
                {
                    node = node.Expand();
                }
                float result = node.State.IsGameOver ? ReversiSimulator.GetResult(node.State) : node.Simulate();
                node.Backpropagate(result);
            }
            return rootNode.GetMostVisitedChild()?.Action;
        }

        /// <summary>
        /// GameStateをNNの入力テンソルに変換する
        /// </summary>
        private float[] ConvertStateToInputTensor(GameState state)
        {
            // 12x12xN チャンネル
            // ch0: 自石(1), ch1: 相手石(1), ch2: 特殊石Type(Normalize), 
            // ch3: 在庫(Extend), ch4: 在庫(Frozen), ... chN: 盤面サイズ

            // 仮実装 (12x12x2 = 自石/相手石)
            int width = GameState.MAX_BOARD_SIZE;
            int height = GameState.MAX_BOARD_SIZE;
            int channels = 2; // (自石, 相手石)
            float[] tensor = new float[width * height * channels];

            StoneColor self = state.CurrentPlayer;
            StoneColor opponent = state.CurrentPlayer.Opponent();

            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    int idx = (r * width + c);
                    if (state.Board[r, c] == self) tensor[idx] = 1.0f;
                    if (state.Board[r, c] == opponent) tensor[idx + (width * height)] = 1.0f;
                }
            }
            return tensor;
        }

        /// <summary>
        /// MCTSの訪問回数をNNのPolicyテンソルに変換する
        /// </summary>
        private float[] ConvertPolicyToTensor(MCTSNode root)
        {
            // 12x12x5 (場所x石タイプ) の確率分布

            // 仮実装 (12x12x5 = 720)
            float[] policy = new float[12 * 12 * 5];
            if (root.Children.Count == 0) return policy;

            float totalVisits = root.Children.Sum(c => c.VisitCount);

            foreach (var child in root.Children)
            {
                if (child.Action == null) continue;

                int posIndex = child.Action.Position.Row * 12 + child.Action.Position.Col;
                int typeIndex = (int)child.Action.Type; // 0~4

                int index = (typeIndex * 144) + posIndex;
                if (index < policy.Length)
                {
                    policy[index] = child.VisitCount / totalVisits;
                }
            }
            return policy;
        }
    }
}
