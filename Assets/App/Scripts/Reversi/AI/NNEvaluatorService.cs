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

				using (var inputTensor = new Tensor(nodesToEvaluate.Count, 12, 12, 2)) // NHWC
				{
					for (int i = 0; i < nodesToEvaluate.Count; i++)
					{
						// NCHW (2, 12, 12) 形式のデータを取得
						float[] stateData = ConvertStateToInputTensor(nodesToEvaluate[i].State);

						// NHWC (12, 12, 2) 形式に並べ替え
						for (int h = 0; h < 12; h++)
							for (int w = 0; w < 12; w++)
							{
								// ch 0 (自石)
								inputTensor[i, h, w, 0] = stateData[(h * 12) + w];
								// ch 1 (相手石)
								inputTensor[i, h, w, 1] = stateData[144 + (h * 12) + w];
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

		/// <pre>
		/// NCHW (Channel first) 形式 (2, 12, 12) のテンソルを生成
		/// [自, 自, ... (144個) ..., 敵, 敵, ... (144個)]
		/// </pre>
		private float[] ConvertStateToInputTensor(GameState state)
		{
			int width = GameState.MAX_BOARD_SIZE;
			int height = GameState.MAX_BOARD_SIZE;
			int channels = 2;
			float[] tensor = new float[width * height * channels];

			StoneColor self = state.CurrentPlayer;
			StoneColor opponent = state.CurrentPlayer.Opponent();

			for (int r = 0; r < height; r++)
			{
				for (int c = 0; c < width; c++)
				{
					int idx = (r * width + c);
					if (state.Board[r, c] == self) tensor[idx] = 1.0f; // Ch 0
					if (state.Board[r, c] == opponent) tensor[idx + (width * height)] = 1.0f; // Ch 1
				}
			}
			return tensor;
		}

		#endregion
	}
}
