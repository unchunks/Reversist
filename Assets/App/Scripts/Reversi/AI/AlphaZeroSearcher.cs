using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace App.Reversi.AI
{
    public static class AlphaZeroSearcher
    {
        public static async UniTask<GameAction> FindBestMove(
            GameState initialState,
            int iterations,
            CancellationToken token)
        {
            var rootNode = new MCTSNode(initialState);

            if (NNEvaluatorService.Instance == null)
            {
                Debug.LogError("NNEvaluatorServiceが見つかりません");
                return null;
            }

            NNEvaluatorService.Instance.EnqueueNode(rootNode);
            await UniTask.WaitUntil(() => rootNode.IsEvaluated(), cancellationToken: token);

            // 並列探索
            int threadsCount = Math.Min(4, Environment.ProcessorCount);
            var tasks = new List<UniTask>();

            for (int t = 0; t < threadsCount; t++)
            {
                tasks.Add(SearchWorker(rootNode, iterations / threadsCount, token));
            }
            await UniTask.WhenAll(tasks);

            if (token.IsCancellationRequested) return null;

            if (rootNode.Children.Count == 0) return null;

            return rootNode.Children
                .OrderByDescending(kvp => kvp.Value.GetVisitCount())
                .First().Key;
        }

        private static async UniTask SearchWorker(
            MCTSNode rootNode,
            int iterations,
            CancellationToken token)
        {
            for (int i = 0; i < iterations; i++)
            {
                if (token.IsCancellationRequested) break;

                var node = rootNode;

                //Debug.Log($"AlphaZeroSearcher: Iteration {i + 1}/{iterations} started.");
                // Selection
                while (!node.IsLeafAndNotEvaluated() && !node.IsGameOver())
                {
                    var action = node.SelectActionByPUCT();
                    if (action == null)
                    {
                        //Debug.Log("AlphaZeroSearcher: No valid action found during selection.");
                        break;
                    }
                    node = node.Expand(action);
                }

                // Evaluation
                if (node.IsLeafAndNotEvaluated())
                {
                    NNEvaluatorService.Instance.EnqueueNode(node);
                    await UniTask.WaitUntil(() => node.IsEvaluated(), cancellationToken: token);
                }
            }
        }
    }
}
