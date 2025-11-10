using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using Unity.Barracuda;

namespace App.Reversi.AI
{
	public class SelfPlayManager : MonoBehaviour
	{
		public enum LearningMode
		{
			/// <summary>
			/// .onnxモデルが無い場合。高速なランダムMCTSで最初の教師データ（棋譜）を生成
			/// </summary>
			CreateInitialData_RandomPlayout,

			/// <summary>
			/// .onnxモデルが既にある場合。GPU/NPUを使い、NNモデルの評価に基づいてより賢く探索（自己対戦）
			/// </summary>
			ImproveModel_NNEvaluation
		}

		[Header("モード設定")]
		[SerializeField] private LearningMode _mode = LearningMode.CreateInitialData_RandomPlayout;

		[Header("学習設定")]
		[SerializeField] private int _gamesToPlay = 1000;
		[SerializeField] private int _mctsIterationsPerMove = 1000;

		[Header("並列設定")]
		[Tooltip("同時に実行するゲーム数。CPUコア数（ProcessorCount）に合わせるのが最適")]
		[SerializeField] private int _parallelGames = 32;

		[SerializeField] private string _outputDirectory = "TrainingData";

		// --- 共通リソース ---
		private CancellationTokenSource _cts;

		private long _gamesCompletedCount = 0;


		[ContextMenu("Start Self Play")]
		public async void StartSelfPlay()
		{
			if (_cts != null && !_cts.IsCancellationRequested)
			{
				Debug.LogWarning("自己対戦はすでに実行中です。");
				return;
			}

			_cts = new CancellationTokenSource();
			_gamesCompletedCount = 0;

			Debug.Log($"自己対戦を開始します。モード: {_mode}, 総ゲーム数: {_gamesToPlay}");

			try
			{
				// I/O処理をtryブロック内のスレッドプールで非同期に実行
				string dirPath = Path.Combine(Application.dataPath, _outputDirectory);
				await UniTask.RunOnThreadPool(() =>
				{
					if (!Directory.Exists(dirPath))
					{
						Directory.CreateDirectory(dirPath);
					}
				}, cancellationToken: _cts.Token);

				Debug.Log($"出力ディレクトリ: {dirPath}");

				var gameTasks = new List<UniTask>();

				gameTasks.Add(LogProgressLoopAsync(_cts.Token));

				if (_mode == LearningMode.ImproveModel_NNEvaluation)
				{
					// Barracudaワーカーのセットアップを待つ代わりに、
					// NNEvaluatorService が利用可能（初期化完了）か確認する
					if (NNEvaluatorService.Instance == null)
					{
						Debug.LogError("NNEvaluatorService がシーンに存在しません。");
						return;
					}
					// (NNEvaluatorServiceのStart()が自動で評価ループを開始する)

					for (int i = 0; i < _parallelGames; i++)
					{
						gameTasks.Add(RunGameLoop_AlphaZero(dirPath, _cts.Token));
					}
					await UniTask.WhenAll(gameTasks);
				}
				else
				{
					for (int i = 0; i < _parallelGames; i++)
					{
						gameTasks.Add(RunGameLoop_ClassicMCTS(dirPath, _cts.Token));
					}
					await UniTask.WhenAll(gameTasks);
				}
			}
			catch (OperationCanceledException)
			{
				Debug.Log("自己対戦が中断されました。");
			}
			catch (Exception e)
			{
				Debug.LogError($"自己対戦中にエラーが発生しました: {e.Message}\n{e.StackTrace}");
			}
			finally
			{
				_cts?.Dispose();
				_cts = null;
				// (BarracudaのリソースはNNEvaluatorServiceが管理)
				Debug.Log("自己対戦プロセスが終了しました。");
			}
		}

		[ContextMenu("Stop Self Play")]
		public void StopSelfPlay()
		{
			if (_cts == null || _cts.IsCancellationRequested)
			{
				Debug.Log("学習は実行されていません。");
				return;
			}
			Debug.LogWarning("学習の中断リクエストを送信しました...");
			_cts.Cancel();
		}

		#region モード1: Classic MCTS (ランダム・プレイアウト)

		private async UniTask RunGameLoop_ClassicMCTS(string dirPath, CancellationToken token)
		{
			Debug.Log("Classic MCTSモードで自己対戦を開始します。");
			await UniTask.SwitchToThreadPool();

			while (Interlocked.Read(ref _gamesCompletedCount) < (long)_gamesToPlay && !token.IsCancellationRequested)
			{
				long gameIndexLong = Interlocked.Increment(ref _gamesCompletedCount) - 1;
				if (gameIndexLong >= _gamesToPlay) break;

				int gameIndex = (int)gameIndexLong;

				(GameRecord record, float finalResult) = RunSingleGame_ClassicMCTS(token);
				if (token.IsCancellationRequested) break;

				// Zipで (TrainingSample, object) のタプルを作成し、正しく受け取る
				foreach (var (sample, rootNodeObject) in record.samples.Zip(record.tempRoots, (s, r) => (s, r)))
				{
					// objectを MCTSNode_Classic にキャスト
					var rootNode = (MCTSNode_Classic)rootNodeObject;
					var gameState = rootNode.State; // ノードからGameStateを取得

					// TrainingSampleの 'value' を計算
					sample.value = (gameState.CurrentPlayer == StoneColor.Black)
						? (finalResult == 0.5f ? 0.0f : (finalResult == 1.0f ? 1.0f : -1.0f))
						: (finalResult == 0.5f ? 0.0f : (finalResult == 1.0f ? -1.0f : 1.0f));
				}

				string filePath = Path.Combine(dirPath, $"game_classic_{gameIndex:D5}.json");
				TrainingDataWriter.Write(record, filePath);

				await UniTask.SwitchToMainThread(PlayerLoopTiming.Update, token);
				Debug.Log($"[Classic] ゲーム {gameIndex} 完了。 (進捗: {_gamesCompletedCount}/{_gamesToPlay})");
				await UniTask.SwitchToThreadPool();
			}
		}

		private (GameRecord record, float finalResult) RunSingleGame_ClassicMCTS(CancellationToken token)
		{
			GameState state = new GameState();
			var gameRecord = new GameRecord();

			while (!state.IsGameOver && !token.IsCancellationRequested)
			{
				MCTSNode_Classic rootNode = new MCTSNode_Classic(state);
				GameAction bestAction = Search_ClassicMCTS(rootNode, _mctsIterationsPerMove, token);

				if (token.IsCancellationRequested) break;

				var sample = new TrainingSample
				{
					inputTensor = ConvertStateToInputTensor(state),
					policy = ConvertPolicyToTensor(rootNode)
				};

				gameRecord.samples.Add(sample);
				gameRecord.tempRoots.Add(rootNode);

				if (bestAction == null) break;
				state = ReversiSimulator.ExecuteAction(state, bestAction);
			}

			return (gameRecord, ReversiSimulator.GetResult(state));
		}

		private GameAction Search_ClassicMCTS(MCTSNode_Classic rootNode, int iterations, CancellationToken token)
		{
			for (int i = 0; i < iterations; i++)
			{
				if (token.IsCancellationRequested) return null;

				MCTSNode_Classic node = rootNode;
				while (!node.HasUntriedActions() && !node.State.IsGameOver)
				{
					node = node.SelectBestChild();
				}
				if (node.HasUntriedActions() && !node.State.IsGameOver)
				{
					node = node.Expand();
				}
				float result = node.State.IsGameOver
					? ReversiSimulator.GetResult(node.State)
					: node.Simulate(token);

				node.Backpropagate(result);
			}
			return rootNode.GetMostVisitedChild()?.Action;
		}

		#endregion

		#region モード2: AlphaZero (NN評価)

		private async UniTask RunGameLoop_AlphaZero(string dirPath, CancellationToken token)
		{
			Debug.Log("AlphaZeroモードで自己対戦を開始します。");
			await UniTask.SwitchToThreadPool();

			while (Interlocked.Read(ref _gamesCompletedCount) < (long)_gamesToPlay && !token.IsCancellationRequested)
			{
				long gameIndexLong = Interlocked.Increment(ref _gamesCompletedCount) - 1;
				if (gameIndexLong >= _gamesToPlay) break;

				int gameIndex = (int)gameIndexLong;

				GameState state = new GameState();
				var gameRecord = new GameRecord();

				// MCTSルートノードを一時的に保持するリスト (Policy計算用)
				var mctsPolicyRoots = new List<(GameState state, MCTSNode_AlphaZero root)>();

				while (!state.IsGameOver && !token.IsCancellationRequested)
				{
					MCTSNode_AlphaZero rootNode = await SearchWithNN(state, _mctsIterationsPerMove, token);
					if (token.IsCancellationRequested) break;

					mctsPolicyRoots.Add((new GameState(state), rootNode));

					if (rootNode.Children.Count == 0) break;
					GameAction bestAction = rootNode.Children.OrderByDescending(kvp => kvp.Value.GetVisitCount()).First().Key;

					state = ReversiSimulator.ExecuteAction(state, bestAction);
				}

				if (token.IsCancellationRequested) break;

				float finalResult = ReversiSimulator.GetResult(state);

				foreach (var (gameState, root) in mctsPolicyRoots)
				{
					var sample = new TrainingSample
					{
						inputTensor = ConvertStateToInputTensor(gameState),
						policy = ConvertPolicyToTensor(root),
						value = (gameState.CurrentPlayer == StoneColor.Black)
							? (finalResult == 0.5f ? 0.0f : (finalResult == 1.0f ? 1.0f : -1.0f))
							: (finalResult == 0.5f ? 0.0f : (finalResult == 1.0f ? -1.0f : 1.0f))
					};
					gameRecord.samples.Add(sample);
				}

				string filePath = Path.Combine(dirPath, $"game_nn_{gameIndex:D5}.json");
				TrainingDataWriter.Write(gameRecord, filePath);

				await UniTask.SwitchToMainThread(PlayerLoopTiming.Update, token);
				Debug.Log($"[AlphaZero] ゲーム {gameIndex} 完了。 (進捗: {_gamesCompletedCount}/{_gamesToPlay})");
				await UniTask.SwitchToThreadPool();
			}
		}

		private async UniTask<MCTSNode_AlphaZero> SearchWithNN(GameState initialState, int iterations, CancellationToken token)
		{
			MCTSNode_AlphaZero rootNode = new MCTSNode_AlphaZero(initialState);

			NNEvaluatorService.Instance.EnqueueNode(rootNode);

			await UniTask.WaitUntil(() => rootNode.IsEvaluated(), cancellationToken: token);

			for (int i = 0; i < iterations; i++)
			{
				if (token.IsCancellationRequested) break;

				MCTSNode_AlphaZero node = rootNode;

				while (!node.IsLeafAndNotEvaluated() && !node.IsGameOver())
				{
					GameAction action = node.SelectActionByPUCT();
					if (action == null) break;
					node = node.Expand(action);
				}

				if (node.IsLeafAndNotEvaluated())
				{
					NNEvaluatorService.Instance.EnqueueNode(node);
				}
			}
			return rootNode;
		}

		#endregion

		#region 共通ヘルパー

		private float[] ConvertStateToInputTensor(GameState state)
		{
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

		private float[] ConvertPolicyToTensor(MCTSNode_Classic root)
		{
			float[] policy = new float[12 * 12 * 5];
			if (root.Children.Count == 0) return policy;

			float totalVisits = root.Children.Sum(c => c.GetVisitCount());
			if (totalVisits == 0) return policy;

			foreach (var child in root.Children)
			{
				if (child.Action == null) continue;

				int posIndex = child.Action.Position.Row * 12 + child.Action.Position.Col;
				int typeIndex = (int)child.Action.Type;
				int index = (typeIndex * 144) + posIndex;

				if (index >= 0 && index < policy.Length)
				{
					policy[index] = child.GetVisitCount() / totalVisits;
				}
			}
			return policy;
		}

		private float[] ConvertPolicyToTensor(MCTSNode_AlphaZero root)
		{
			float[] policy = new float[12 * 12 * 5];
			if (root.Children.Count == 0) return policy;

			float totalVisits = root.Children.Sum(kvp => kvp.Value.GetVisitCount());
			if (totalVisits == 0) return policy;

			foreach (var kvp in root.Children)
			{
				GameAction action = kvp.Key;
				MCTSNode_AlphaZero child = kvp.Value;

				int posIndex = action.Position.Row * 12 + action.Position.Col;
				int typeIndex = (int)action.Type;
				int index = (typeIndex * 144) + posIndex;

				if (index >= 0 && index < policy.Length)
				{
					policy[index] = child.GetVisitCount() / totalVisits;
				}
			}
			return policy;
		}

		/// <summary>
		/// メインスレッドで定期的に進捗をログ出力する
		/// （メインスレッドがフリーズしていないことを確認する）
		/// </summary>
		private async UniTask LogProgressLoopAsync(CancellationToken token)
		{
			// このタスクが確実にメインスレッドで実行されるようにする
			await UniTask.SwitchToMainThread(token);

			Debug.Log("進捗ログの定期出力を開始します。");

			while (!token.IsCancellationRequested)
			{
				try
				{
					// 5秒間、メインスレッドをブロックせずに待機
					await UniTask.Delay(TimeSpan.FromSeconds(5.0), cancellationToken: token);

					// キャンセルチェック (Delayが例外を投げなかった場合)
					if (token.IsCancellationRequested) break;

					// Interlocked.Readでスレッドセーフに読み取る
					long completed = Interlocked.Read(ref _gamesCompletedCount);
					Debug.Log($"自己対戦 実行中... (進捗: {completed}/{_gamesToPlay})");
				}
				catch (OperationCanceledException)
				{
					// Delay中にキャンセルされたらループを抜ける
					break;
				}
			}
		}

		#endregion
	}
}
