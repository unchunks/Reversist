using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Unity.Barracuda;

namespace App.Reversi.AI
{
	/// <summary>
	/// Barracuda(ONNX)の推論をバッチ処理で実行する常駐サービス
	/// (シングルトンとしてシーンに1つだけ配置する)
	/// </summary>
	public class NNEvaluatorService : MonoBehaviour
	{
		public static NNEvaluatorService Instance { get; private set; }

		[Header("NNモデル")]
		[SerializeField] private NNModel _nnModelAsset;
		[SerializeField] private int _maxBatchSize = 64;

		private Model _runtimeModel;
		private IWorker _worker;
		private ConcurrentQueue<MCTSNode_AlphaZero> _evaluationQueue;
		private CancellationTokenSource _cts;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}

		private async void Start()
		{
			_cts = new CancellationTokenSource();
			_evaluationQueue = new ConcurrentQueue<MCTSNode_AlphaZero>();

			if (!await SetupBarracudaWorkerAsync(_cts.Token))
			{
				Debug.LogError("NNEvaluatorServiceの初期化に失敗しました。");
				enabled = false;
				return;
			}

			RunEvaluationBatchLoopAsync(_cts.Token).Forget();
		}

		private void OnDestroy()
		{
			_cts?.Cancel();
			CleanupResources();
			if (Instance == this)
			{
				Instance = null;
			}
		}

		public void EnqueueNode(MCTSNode_AlphaZero node)
		{
			if (_evaluationQueue == null || _cts.IsCancellationRequested) return;
			_evaluationQueue.Enqueue(node);
		}

		#region Barracuda Worker & Batch Loop

		private async UniTask RunEvaluationBatchLoopAsync(CancellationToken token)
		{
			var nodesToEvaluate = new List<MCTSNode_AlphaZero>(_maxBatchSize);

			while (!token.IsCancellationRequested)
			{
				nodesToEvaluate.Clear();

				MCTSNode_AlphaZero firstNode;
				while (!_evaluationQueue.TryDequeue(out firstNode))
				{
					if (token.IsCancellationRequested) return;
					await UniTask.Delay(1, cancellationToken: token);
				}
				nodesToEvaluate.Add(firstNode);

				while (nodesToEvaluate.Count < _maxBatchSize && _evaluationQueue.TryDequeue(out MCTSNode_AlphaZero node))
				{
					nodesToEvaluate.Add(node);
				}

				using (var inputTensor = new Tensor(nodesToEvaluate.Count, 12, 12, 2))
				{
					int sampleSize = 12 * 12 * 2; // 288

					for (int i = 0; i < nodesToEvaluate.Count; i++)
					{
						// NHWC (12, 12, 2) 形式のデータを取得
						float[] stateData = ConvertStateToInputTensor(nodesToEvaluate[i].State);

						// BarracudaのTensor (NHWC) に
						// 1次元配列 (NHWC) からデータを直接コピー
						// (i番目のバッチのj番目の要素としてフラットに書き込む)
						for (int j = 0; j < sampleSize; j++)
						{
							inputTensor[i, j] = stateData[j];
						}
					}

					_worker.Execute(inputTensor);

					// Pythonで "policy", "value" と名付けた出力を取得
					Tensor policyTensor = _worker.PeekOutput("policy");
					Tensor valueTensor = _worker.PeekOutput("value");

					int policySize = 12 * 12 * 5; // 720
					float[] allPolicyData = policyTensor.AsFloats();
					float[] allValueData = valueTensor.AsFloats();

					for (int i = 0; i < nodesToEvaluate.Count; i++)
					{
						float[] policy = new float[policySize];
						Array.Copy(allPolicyData, i * policySize, policy, 0, policySize);
						float value = allValueData[i];

						nodesToEvaluate[i].SetEvaluationResult(policy, value);
					}

					policyTensor.Dispose();
					valueTensor.Dispose();
				}
			}
		}

		private async UniTask<bool> SetupBarracudaWorkerAsync(CancellationToken token)
		{
			try
			{
				Debug.Log("Barracudaワーカーの作成を開始します (メインスレッド)...");

				var type = WorkerFactory.Type.Auto;
				var model = ModelLoader.Load(_nnModelAsset);
				var wrkr = WorkerFactory.CreateWorker(type, model);

				// 念のため、初期化が完了するまで1フレーム待機します
				await UniTask.Yield(PlayerLoopTiming.Update, token);

				_runtimeModel = model;
				_worker = wrkr;

				Debug.Log($"Barracudaワーカーを {WorkerFactory.Type.Auto} で作成しました。");
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"NNモデルのロードまたはワーカーの作成に失敗: {e.Message}");
				return false;
			}
		}

		private void CleanupResources()
		{
			_worker?.Dispose();
			_worker = null;
			_runtimeModel = null;
			_cts?.Dispose();
			_cts = null;
		}

		/// <summary>
		/// NHWC (Height, Width, Channel) 形式 (12, 12, 2) のテンソルを生成
		/// [自, 敵, 自, 敵, ... (288個)]
		/// </summary>
		/// <param name="state"></param>
		private float[] ConvertStateToInputTensor(GameState state)
		{
			int width = GameState.MAX_BOARD_SIZE;    // 12
			int height = GameState.MAX_BOARD_SIZE;   // 12
			int channels = 2; // (自石, 相手石)
			float[] tensor = new float[width * height * channels]; // 288

			StoneColor self = state.CurrentPlayer;
			StoneColor opponent = state.CurrentPlayer.Opponent(); //

			for (int r = 0; r < height; r++)
			{
				for (int c = 0; c < width; c++)
				{
					// NHWC: (Height, Width, Channel) の順でインデックスを計算
					int base_idx = (r * width + c) * channels; // (r * 12 + c) * 2

					if (state.Board[r, c] == self)
					{
						tensor[base_idx + 0] = 1.0f; // Channel 0 (自石)
					}
					if (state.Board[r, c] == opponent)
					{
						tensor[base_idx + 1] = 1.0f; // Channel 1 (相手石)
					}
				}
			}
			return tensor;
		}

		#endregion
	}
}
