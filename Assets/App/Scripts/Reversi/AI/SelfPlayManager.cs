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
	/// <summary>
	/// AIの自己対戦をGPU/NPUバッチ処理で並列実行し、教師データ（棋譜）を生成する
	/// </summary>
	public class SelfPlayManager : MonoBehaviour
	{
		[Header("学習設定")]
		[SerializeField] private int _gamesToPlay = 1000;
		[SerializeField] private int _mctsIterationsPerMove = 800; // 1手あたりのMCTS探索回数

		[Header("NNモデル")]
		[SerializeField] private NNModel _nnModelAsset; // .onnxモデルファイルをInspectorから設定
		[SerializeField] private int _maxBatchSize = 64; // GPUに一度に送る盤面の数

		[Header("並列設定")]
		[Tooltip("同時に実行するゲーム数。CPUコア数（ProcessorCount）に合わせるのが最適")]
		[SerializeField] private int _parallelGames = 32;

		[SerializeField] private string _outputDirectory = "TrainingData";

		// NN推論エンジン
		private Model _runtimeModel;
		private IWorker _worker;

		// スレッドセーフな進捗カウンタ
		private int _gamesCompletedCount = 0;

		// NN評価待ちのノードを全スレッドで共有するキュー
		private ConcurrentQueue<MCTSNode> _evaluationQueue;
		private CancellationTokenSource _cts;

		[ContextMenu("Start Self Play")]
		public async void StartSelfPlay()
		{
			// Barracuda (NNエンジン) のセットアップ
			try
			{
				_runtimeModel = ModelLoader.Load(_nnModelAsset);
				// GPU (ComputePrecompiled) を優先し、なければNPU(Fast)やCPU(CSharp)を使う
				_worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, _runtimeModel);
				Debug.Log($"Barracudaワーカーを {WorkerFactory.Type.Auto} で作成しました。");
			}
			catch (Exception e)
			{
				Debug.LogError($"NNモデルのロードまたはワーカーの作成に失敗しました: {e.Message}");
				return;
			}

			// 共有リソースの初期化
			_cts = new CancellationTokenSource();
			_evaluationQueue = new ConcurrentQueue<MCTSNode>();
			_gamesCompletedCount = 0;

			string dirPath = Path.Combine(Application.dataPath, _outputDirectory);
			if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

			Debug.Log($"自己対戦を開始します。総ゲーム数: {_gamesToPlay}, 並列ゲーム数: {_parallelGames}");

			try
			{
				// GPU/NPUで推論バッチ処理を行うタスクを開始（1つだけ）
				UniTask evaluationTask = RunEvaluationBatchLoopAsync(_cts.Token);

				// CPUでMCTS探索を行うゲーム実行タスクを開始（_parallelGamesの数だけ）
				var gameTasks = new List<UniTask>();
				for (int i = 0; i < _parallelGames; i++)
				{
					gameTasks.Add(RunGameSimulationLoopAsync(dirPath, _cts.Token));
				}

				// すべて完了するのを待つ
				await UniTask.WhenAll(gameTasks.Concat(new[] { evaluationTask }));
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
				_worker?.Dispose();
				_cts?.Dispose();
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

		/// <summary>
		/// （CPUスレッド）1つのスレッドがゲームのシミュレーションを繰り返し実行する
		/// </summary>
		private async UniTask RunGameSimulationLoopAsync(string dirPath, CancellationToken token)
		{
			// このスレッドが起動してから、全ゲーム数が完了するまで動き続ける
			while (Volatile.Read(ref _gamesCompletedCount) < _gamesToPlay && !token.IsCancellationRequested)
			{
				// スレッドセーフにゲーム番号を取得
				int gameIndex = Interlocked.Increment(ref _gamesCompletedCount) - 1;
				if (gameIndex >= _gamesToPlay) break;

				GameState state = new GameState();
				var gameRecord = new GameRecord();
				var mctsPolicies = new List<(GameState state, MCTSNode root)>();

				while (!state.IsGameOver && !token.IsCancellationRequested)
				{
					// MCTS探索（NN評価はキューイングされる）
					MCTSNode rootNode = await SearchWithNN(state, _mctsIterationsPerMove, token);

					if (token.IsCancellationRequested) break;

					mctsPolicies.Add((new GameState(state), rootNode));

					// 最も訪問回数が多かった手を選択（温度パラメータ=0）
					if (rootNode.Children.Count == 0) break; // 手がない
					GameAction bestAction = rootNode.Children.OrderByDescending(kvp => kvp.Value.GetVisitCount()).First().Key;

					state = ReversiSimulator.ExecuteAction(state, bestAction);
				}

				if (token.IsCancellationRequested) break;

				// --- ゲーム終了 ---
				float finalResult = ReversiSimulator.GetResult(state);

				// 棋譜データ(GameRecord)を構築
				foreach (var (gameState, root) in mctsPolicies)
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

				// ファイルに書き出し
				string filePath = Path.Combine(dirPath, $"game_{gameIndex:D5}.json");
				TrainingDataWriter.Write(gameRecord, filePath);

				// メインスレッドに進捗を報告
				await UniTask.SwitchToMainThread(PlayerLoopTiming.Update, token);
				Debug.Log($"ゲーム {gameIndex} 完了。 (進捗: {_gamesCompletedCount}/{_gamesToPlay})");
				await UniTask.SwitchToThreadPool(); // スレッドプールのループに戻る
			}
		}

		/// <summary>
		/// （GPU/NPUスレッド）評価キューを監視し、バッチ処理を実行するループ
		/// </summary>
		private async UniTask RunEvaluationBatchLoopAsync(CancellationToken token)
		{
			var nodesToEvaluate = new List<MCTSNode>(_maxBatchSize);

			while (!token.IsCancellationRequested)
			{
				// キューからノードを取得（バッチサイズ分、またはタイムアウトまで）
				nodesToEvaluate.Clear();

				// 最初の1個を取得（キューが空なら待機）
				MCTSNode firstNode;
				while (!_evaluationQueue.TryDequeue(out firstNode))
				{
					if (token.IsCancellationRequested) return;
					await UniTask.Delay(1, cancellationToken: token); // 1ms待機
				}
				nodesToEvaluate.Add(firstNode);

				// 残りをバッチサイズまで取得
				while (nodesToEvaluate.Count < _maxBatchSize && _evaluationQueue.TryDequeue(out MCTSNode node))
				{
					nodesToEvaluate.Add(node);
				}

				// バッチテンソルを作成
				// (注：モデルの入力形状 [B,C,H,W] または [B,H,W,C] に合わせる)
				// 仮：[Batch, 12, 12, 2]
				var inputTensor = new Tensor(nodesToEvaluate.Count, 12, 12, 2);
				for (int i = 0; i < nodesToEvaluate.Count; i++)
				{
					float[] stateData = ConvertStateToInputTensor(nodesToEvaluate[i].State);
					for (int j = 0; j < stateData.Length; j++)
					{
						inputTensor[i, j] = stateData[j];
					}
				}

				// GPU/NPUでバッチ推論を実行（同期）
				_worker.Execute(inputTensor);

				// 結果を取得
				Tensor policyTensor = _worker.PeekOutput("Policy_Output_Name"); // モデルの出力名に合わせる
				Tensor valueTensor = _worker.PeekOutput("Value_Output_Name");   // モデルの出力名に合わせる

				// 結果を各ノードに設定（MCTSのBackpropagateが実行される）
				for (int i = 0; i < nodesToEvaluate.Count; i++)
				{
					float[] policy = policyTensor.AsFloats();
					float value = valueTensor.AsFloats()[0];

					// SetEvaluationResultはスレッドセーフ（lockあり）
					nodesToEvaluate[i].SetEvaluationResult(policy, value);
				}

				inputTensor.Dispose();
				policyTensor.Dispose();
				valueTensor.Dispose();
			}
		}

		/// <summary>
		/// MCTS探索を実行し、NN評価が必要なノードをキューに追加する
		/// </summary>
		private async UniTask<MCTSNode> SearchWithNN(GameState initialState, int iterations, CancellationToken token)
		{
			MCTSNode rootNode = new MCTSNode(initialState);

			// 最初のノード（ルート）を評価待ちキューに追加
			_evaluationQueue.Enqueue(rootNode);

			// ルートノードの評価が完了するまで待機
			await UniTask.WaitUntil(() => rootNode.IsEvaluated(), cancellationToken: token);

			for (int i = 0; i < iterations; i++)
			{
				if (token.IsCancellationRequested) break;

				MCTSNode node = rootNode;

				// 1. Selection & Expansion
				while (!node.IsLeafAndNotEvaluated() && !node.IsGameOver())
				{
					GameAction action = node.SelectActionByPUCT();
					node = node.Expand(action);
				}

				// 2. 葉ノードに到達したら評価キューに追加
				if (node.IsLeafAndNotEvaluated())
				{
					_evaluationQueue.Enqueue(node);
				}
			}
			return rootNode;
		}

		// --- テンソル変換メソッド ---

		/// <summary>
		/// GameStateをNNの入力テンソル（float[]）に変換する
		/// </summary>
		private float[] ConvertStateToInputTensor(GameState state)
		{
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
		/// MCTSの訪問回数をNNのPolicyテンソル（float[]）に変換する
		/// </summary>
		private float[] ConvertPolicyToTensor(MCTSNode root)
		{
			// 仮実装 (12x12x5 = 720)
			float[] policy = new float[12 * 12 * 5];
			if (root.Children.Count == 0) return policy;

			float totalVisits = root.Children.Sum(c => c.Value.GetVisitCount());
			if (totalVisits == 0) return policy;

			foreach (var kvp in root.Children)
			{
				GameAction action = kvp.Key;
				MCTSNode child = kvp.Value;

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
	}
}
