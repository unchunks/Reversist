using App.Reversi.AI;
using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
	public class AIAgent : MonoBehaviour
	{
		[Header("AI設定")]
		[Tooltip("AIが1手に行うMCTSの反復回数")]
		[SerializeField] private int _mctsIterationsPerMove = 2000;

		[Inject] private Board _board;
		[Inject] private PlayerInventory _playerInventory;

		[Inject] private IPublisher<RequestPutStoneMessage> _requestPutStonePublisher;
		[Inject] private IPublisher<AIThinkingMessage> _aiThinkingPublisher;

		[Inject] private ISubscriber<TurnChangedMessage> _turnChangedSubscriber;

		private StoneColor _aiColor = StoneColor.None;
		private bool _isAITurn = false;

		[Inject]
		private void Construct()
		{
			_turnChangedSubscriber.Subscribe(OnTurnChanged);
		}

		public void Initialize(StoneColor aiColor)
		{
			_aiColor = aiColor;
		}

		private void OnTurnChanged(TurnChangedMessage msg)
		{
			if (_aiColor == StoneColor.None)
			{
				Debug.LogWarning("AIの色が設定されていません。");
				return;
			}
			if (msg.CurrentPlayer == _aiColor)
			{
				_isAITurn = true;
				RequestAIMove().Forget();
			}
			else
			{
				_isAITurn = false;
			}
		}

		private async UniTask RequestAIMove()
		{
			_aiThinkingPublisher.Publish(new AIThinkingMessage(_aiColor, true));

			GameState currentState = BuildCurrentGameState();

			GameAction bestAction = await AlphaZeroSearcher.FindBestMove(
				currentState,
				_mctsIterationsPerMove,
				this.GetCancellationTokenOnDestroy()
			);
			Debug.Log($"AI({_aiColor})の選択: Position={bestAction?.Position}, Type={bestAction?.Type}");

			currentState.ReturnToPool(); // プールに返す

			_aiThinkingPublisher.Publish(new AIThinkingMessage(_aiColor, false));

			if (!_isAITurn) return;
			if (bestAction != null)
			{
				_board.HideHighlight();
				_requestPutStonePublisher.Publish(new RequestPutStoneMessage(bestAction.Player, bestAction.Type, bestAction.Position));
			}
		}

		private GameState BuildCurrentGameState()
		{
			// BoardStateConverterの拡張メソッドを使い、UnityのBoardから最適化されたGameStateを生成
			return BoardStateConverter.GetCurrentGameState(_board, _aiColor, _playerInventory.Inventories);
		}
	}
}
