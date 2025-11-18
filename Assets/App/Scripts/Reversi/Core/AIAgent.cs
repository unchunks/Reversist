using App.Reversi.AI;
using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using MessagePipe;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
    public class AIAgent : MonoBehaviour
    {

        [Inject] private Board _board;
        [Inject] private PlayerInventory _playerInventory;

        [Inject] private IPublisher<RequestPutStoneMessage> _requestPutStonePublisher;
        [Inject] private IPublisher<AIThinkingMessage> _aiThinkingPublisher;

        [Inject] private ISubscriber<TurnChangedMessage> _turnChangedSubscriber;

        private int _thinkingTimeMilliseconds = ToReversiValues.AiThinkTime; // 思考時間

        private StoneColor _aiColor = StoneColor.None;
        private bool _isAITurn = false;
        private MCTS mctsAlgorithm = new MCTS();

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

            // MCTS実行
            MCTSSearchResult result = await mctsAlgorithm.Search(
                currentState,
                _thinkingTimeMilliseconds
            ).AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

            // ログは削除しました

            GameAction bestAction = result.BestAction;

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
            var inventoriesCopy = new Dictionary<StoneColor, AvailableStoneCount>();
            foreach (var inv in _playerInventory.Inventories)
            {
                inventoriesCopy.Add(inv.Key, new AvailableStoneCount(inv.Value));
            }

            var state = new GameState
            {
                CurrentPlayer = _aiColor,
                CurrentBoardSize = _board.CurrentBoardSize,
                Inventories = inventoriesCopy,
                DelayReverseStack = new List<ReverseCountDown>(_board.DelayReverseStack),
                IsGameOver = false,
                StoneCount = new Dictionary<StoneColor, int>() {
                    { StoneColor.Black, 0 }, { StoneColor.White, 0 }
                }
            };

            for (int r = 0; r < GameState.MAX_BOARD_SIZE; r++)
            {
                for (int c = 0; c < GameState.MAX_BOARD_SIZE; c++)
                {
                    Cell cell = _board.BoardCells[r, c];
                    if (cell != null && cell.isPlased)
                    {
                        state.Board[r, c] = cell.Color;
                        state.StoneTypes[r, c] = cell.Type;
                        state.StoneCount[cell.Color]++;
                    }
                }
            }
            return state;
        }
    }
}
