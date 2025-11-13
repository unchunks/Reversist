using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Unity.Barracuda;
using UnityEngine;

namespace App.Reversi.AI
{
	public class OptimizedNNEvaluator : MonoBehaviour
	{
		public static OptimizedNNEvaluator Instance { get; private set; }

		[SerializeField] private NNModel _nnModelAsset;
		[SerializeField] private int _maxBatchSize = 128; // バッチサイズ増加

		private Model _runtimeModel;
		private IWorker _worker;
		private ConcurrentQueue<MCTSNode> _evaluationQueue;
		private CancellationTokenSource _cts;

		// テンソル再利用
		private Tensor _reusableTensor;
		private float[] _tensorBuffer;

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
			_tensorBuffer = new float[_maxBatchSize * 12 * 12 * 2];

			if (!await SetupBarracudaWorkerAsync(_cts.Token))
			{
				Debug.LogError("NNEvaluator初期化失敗");
				enabled = false;
				return;
			}

			// 並列評価ループを複数起動
			for (int i = 0; i < 2; i++)
			{
				RunEvaluationBatchLoopAsync(_cts.Token).Forget();
			}
		}

		private void OnDestroy()
		{
			_cts?.Cancel();
			_reusableTensor?.Dispose();
			_worker?.Dispose();
			if (Instance == this) Instance = null;
		}

		public void EnqueueNode(MCTSNode node)
		{
			if (_evaluationQueue == null || _cts.IsCancellationRequested) return;
			_evaluationQueue.Enqueue(node);
		}

		private async UniTask RunEvaluationBatchLoopAsync(CancellationToken token)
		{
			var nodesToEvaluate = new List<MCTSNode>(_maxBatchSize);

			while (!token.IsCancellationRequested)
			{
				nodesToEvaluate.Clear();

				// バッチを収集
				while (nodesToEvaluate.Count < _maxBatchSize &&
					   _evaluationQueue.TryDequeue(out var node))
				{
					nodesToEvaluate.Add(node);
				}

				if (nodesToEvaluate.Count == 0)
				{
					await UniTask.Delay(1, cancellationToken: token);
					continue;
				}

				// バッチ推論実行
				await EvaluateBatch(nodesToEvaluate, token);
			}
		}

		private async UniTask EvaluateBatch(List<MCTSNode> nodes, CancellationToken token)
		{
			int batchSize = nodes.Count;

			using (var inputTensor = new Tensor(batchSize, 12, 12, 2))
			{
				// 入力データを一括コピー
				for (int i = 0; i < batchSize; i++)
				{
					ConvertStateToTensor(nodes[i].State, _tensorBuffer, i * 288);
				}

				// Tensorにコピー
				for (int i = 0; i < batchSize * 288; i++)
				{
					inputTensor[i] = _tensorBuffer[i];
				}

				_worker.Execute(inputTensor);

				var policyTensor = _worker.PeekOutput("policy");
				var valueTensor = _worker.PeekOutput("value");

				var allPolicy = policyTensor.AsFloats();
				var allValue = valueTensor.AsFloats();

				// 結果を各ノードに設定
				for (int i = 0; i < batchSize; i++)
				{
					float[] policy = new float[720];
					Array.Copy(allPolicy, i * 720, policy, 0, 720);
					nodes[i].SetEvaluationResult(policy, allValue[i]);
				}

				policyTensor.Dispose();
				valueTensor.Dispose();
			}

			await UniTask.Yield(PlayerLoopTiming.Update, token);
		}

		private void ConvertStateToTensor(GameState state, float[] buffer, int offset)
		{
			StoneColor self = state.CurrentPlayer;
			StoneColor opponent = self.Opponent();

			for (int r = 0; r < 12; r++)
			{
				for (int c = 0; c < 12; c++)
				{
					int baseIdx = offset + (r * 12 + c) * 2;
					StoneColor stone = state.GetStone(r, c);

					buffer[baseIdx] = stone == self ? 1.0f : 0.0f;
					buffer[baseIdx + 1] = stone == opponent ? 1.0f : 0.0f;
				}
			}
		}

		private async UniTask<bool> SetupBarracudaWorkerAsync(CancellationToken token)
		{
			try
			{
				var model = ModelLoader.Load(_nnModelAsset);
				_worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
				_runtimeModel = model;
				await UniTask.Yield(PlayerLoopTiming.Update, token);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"Barracuda初期化失敗: {e.Message}");
				return false;
			}
		}
	}
}
