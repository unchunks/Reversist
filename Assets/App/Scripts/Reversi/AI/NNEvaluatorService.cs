using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Unity.Barracuda;

namespace App.Reversi.AI
{
	public class NNEvaluatorService : MonoBehaviour
	{
		public static NNEvaluatorService Instance { get; private set; }

		[Header("NNモデル")]
		[SerializeField] private NNModel _nnModelAsset;
		[SerializeField] private int _maxBatchSize = 64;

		private Model _runtimeModel;
		private IWorker _worker;
		private ConcurrentQueue<MCTSNode> _evaluationQueue;
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
			_evaluationQueue = new ConcurrentQueue<MCTSNode>();

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

		public void EnqueueNode(MCTSNode node)
		{
			if (_evaluationQueue == null || _cts.IsCancellationRequested) return;
			_evaluationQueue.Enqueue(node);
		}

		#region Barracuda Worker & Batch Loop

		private async UniTask RunEvaluationBatchLoopAsync(CancellationToken token)
		{
			var nodesToEvaluate = new List<MCTSNode>(_maxBatchSize);

			while (!token.IsCancellationRequested)
			{
				nodesToEvaluate.Clear();

				MCTSNode firstNode;
				while (!_evaluationQueue.TryDequeue(out firstNode))
				{
					if (token.IsCancellationRequested) return;
					await UniTask.Delay(1, cancellationToken: token);
				}
				nodesToEvaluate.Add(firstNode);

				while (nodesToEvaluate.Count < _maxBatchSize && _evaluationQueue.TryDequeue(out MCTSNode node))
				{
					nodesToEvaluate.Add(node);
				}

				int batchCount = nodesToEvaluate.Count;
				const int inputSizePerNode = 12 * 12 * 2;
				float[] batchInputData = new float[batchCount * inputSizePerNode];

				for (int i = 0; i < batchCount; i++)
				{
					float[] stateData = ConvertStateToInputTensor(nodesToEvaluate[i].State);
					Array.Copy(stateData, 0, batchInputData, i * inputSizePerNode, inputSizePerNode);
				}

				using (var inputTensor = new Tensor(batchCount, 12, 12, 2, batchInputData))
				{
					_worker.Execute(inputTensor);

					Tensor policyTensor = _worker.PeekOutput("policy");
					Tensor valueTensor = _worker.PeekOutput("value");

					int policySize = 12 * 12 * 5;
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
				var type = WorkerFactory.Type.Auto;
				var model = ModelLoader.Load(_nnModelAsset);
				var wrkr = WorkerFactory.CreateWorker(type, model);
				await UniTask.Yield(PlayerLoopTiming.Update, token);
				_runtimeModel = model;
				_worker = wrkr;
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

		private float[] ConvertStateToInputTensor(GameState state)
		{
			const int width = GameState.MAX_BOARD_SIZE;
			const int height = GameState.MAX_BOARD_SIZE;
			const int channels = 2;
			float[] tensor = new float[width * height * channels];

			ulong[] selfStones = (state.CurrentPlayer == StoneColor.Black) ? state.BlackStones : state.WhiteStones;
			ulong[] opponentStones = (state.CurrentPlayer == StoneColor.Black) ? state.WhiteStones : state.BlackStones;

			for (int i = 0; i < width * height; i++)
			{
				int arrayIndex = i / 64;
				int bitIndex = i % 64;
				int baseIdx = i * channels;

				if (((selfStones[arrayIndex] >> bitIndex) & 1) == 1)
				{
					tensor[baseIdx + 0] = 1.0f;
				}
				if (((opponentStones[arrayIndex] >> bitIndex) & 1) == 1)
				{
					tensor[baseIdx + 1] = 1.0f;
				}
			}
			return tensor;
		}

		#endregion
	}
}
