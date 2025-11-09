using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Reversi.AI
{
	/// <summary>
	/// AlphaZero型MCTSのノード。NNによるPolicyとValueを持つ。
	/// </summary>
	public class MCTSNode
	{
		// PUCT計算用の探索定数 (AlphaZero論文の値)
		private const double C_PUCT = 4.0;

		public GameState State { get; }
		public MCTSNode Parent { get; }
		public Dictionary<GameAction, MCTSNode> Children { get; }

		public int VisitCount { get; private set; }
		private double _winScore; // このノードの累計勝率（Value）
		private readonly object _lock = new object(); // スレッドセーフ用

		// NNから受け取った評価値
		private readonly float[] _policy; // NNが評価したPolicy（各手の有望度）
		private float _value;     // NNが評価したValue（この盤面の勝率）

		private readonly List<GameAction> _validActions; // このノードで有効な手
		private bool _isEvaluated = false; // NNによる評価が完了したか

		/// <summary>
		/// ノードのコンストラクタ
		/// </summary>
		public MCTSNode(GameState state, MCTSNode parent = null)
		{
			State = state;
			Parent = parent;
			Children = new Dictionary<GameAction, MCTSNode>();
			VisitCount = 0;
			_winScore = 0.0;
			_value = 0.0f;
			_isEvaluated = false;

			// Policy配列を初期化（NNモデルの出力サイズに合わせる）
			// 仮：12x12x5 (場所x石タイプ)
			_policy = new float[12 * 12 * 5];

			if (!state.IsGameOver)
			{
				// 有効な手をキャッシュ（シミュレーターのキャッシュを利用）
				_validActions = ReversiSimulator.GetValidActions(state);
			}
			else
			{
				_validActions = new List<GameAction>();
				_isEvaluated = true; // 終了ノードは評価済みとする
				_value = ReversiSimulator.GetResult(state); // 最終結果をValueとする
			}
		}

		/// <summary>
		/// PUCTスコアが最も高い行動（手）を選択する (Selection)
		/// </summary>
		public GameAction SelectActionByPUCT()
		{
			lock (_lock)
			{
				// すべての有効な手についてPUCTスコアを計算し、最大の手を返す
				return _validActions.OrderByDescending(GetPUCTScore).First();
			}
		}

		/// <summary>
		/// PUCT (Polynomial Upper Confidence Trees) スコアを計算する
		/// </summary>
		private double GetPUCTScore(GameAction action)
		{
			double qValue;
			int childVisitCount;

			if (Children.TryGetValue(action, out MCTSNode child))
			{
				childVisitCount = child.VisitCount;
				qValue = (childVisitCount > 0)
					? (child._winScore / childVisitCount) // 子ノードの平均勝率
					: 0.0;
			}
			else
			{
				// 子ノードがまだ存在しない（= 訪問回数0）
				childVisitCount = 0;
				qValue = 0.0;
			}

			// 親ノード（自分）のNN Policyから、この行動(action)の確率を取得
			double priorPolicy = GetPriorPolicyForAction(action);

			// (平均勝率 Q) + (探索ボーナス U)
			// U = C * P(s,a) * (sqrt(親の総訪問回数) / (1 + 子の訪問回数))
			double uValue = C_PUCT * priorPolicy * (Math.Sqrt(VisitCount) / (1.0 + childVisitCount));

			// 相手視点のQValueを自分視点に反転させる
			return -qValue + uValue;
		}

		/// <summary>
		/// 指定された手（Action）の子ノードを作成または取得する (Expansion)
		/// </summary>
		public MCTSNode Expand(GameAction action)
		{
			lock (_lock)
			{
				if (Children.TryGetValue(action, out MCTSNode child))
				{
					return child; // 既に存在すればそれを返す
				}

				// 存在しなければ新しく作成
				GameState nextState = ReversiSimulator.ExecuteAction(State, action);
				MCTSNode newChild = new MCTSNode(nextState, this);
				Children.Add(action, newChild);
				return newChild;
			}
		}

		/// <summary>
		/// NNの推論結果（PolicyとValue）をこのノードに設定する
		/// </summary>
		public void SetEvaluationResult(float[] policy, float value)
		{
			lock (_lock)
			{
				if (_isEvaluated) return; // 二重評価を防ぐ

				_isEvaluated = true;
				Array.Copy(policy, _policy, policy.Length);
				_value = value;

				// NNの評価（Value）を使ってBackpropagate
				Backpropagate(value);
			}
		}

		/// <summary>
		/// NNの評価（Value）を親ノードへ逆伝播させる
		/// </summary>
		public void Backpropagate(float value)
		{
			MCTSNode node = this;
			while (node != null)
			{
				lock (node._lock)
				{
					node.VisitCount++;
					// 常に「相手」の視点のValueを加算する（= 自分のValueのマイナス値）
					node._winScore += value;
				}
				value = -value; // 伝播するValueを反転させる
				node = node.Parent;
			}
		}

		public bool IsLeafAndNotEvaluated()
		{
			lock (_lock)
			{
				// 子がいない（=葉）かつ、NN評価がまだされていない
				return Children.Count == 0 && !_isEvaluated;
			}
		}

		public bool IsGameOver() => State.IsGameOver;
		public int GetVisitCount() => VisitCount;
		public bool IsEvaluated() => _isEvaluated;
		/// <summary>
		/// まだ試していない（展開していない）有効な手が1つでも残っているか
		/// </summary>
		public bool HasUntriedActions() => _validActions.Count > 0;

		/// <summary>
		/// NNが評価したPolicy配列から、特定の手の確率を取得する
		/// </summary>
		private float GetPriorPolicyForAction(GameAction action)
		{
			try
			{
				int posIndex = action.Position.Row * 12 + action.Position.Col;
				int typeIndex = (int)action.Type; // 0~4
				int index = (typeIndex * 144) + posIndex; // 5 * (12*12)

				if (index >= 0 && index < _policy.Length)
				{
					return _policy[index];
				}
				return 0.0f;
			}
			catch (Exception)
			{
				return 0.0f; // 安全装置
			}
		}
	}
}
