using App.Reversi.AI;
using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using MessagePipe;
using System.Collections.Generic;
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

        /// <summary>
        /// GameControllerからAIの色を設定する
        /// </summary>
        public void Initialize(StoneColor aiColor)
        {
            _aiColor = aiColor;
        }

        /// <summary>
        /// ターン変更の通知を受け取る
        /// </summary>
        private void OnTurnChanged(TurnChangedMessage msg)
        {
            if (msg.CurrentPlayer == _aiColor)
            {
                _isAITurn = true;
                // 思考開始（UniTaskをForgetで非同期実行）
                RequestAIMove().Forget();
            }
            else
            {
                _isAITurn = false;
            }
        }

        /// <summary>
        /// AIの思考と行動実行
        /// </summary>
        private async UniTask RequestAIMove()
        {
            // UIに「思考中」を通知
            Debug.Log("AI思考開始");
            _aiThinkingPublisher.Publish(new AIThinkingMessage(_aiColor, true));

            // 現在のUnityの盤面から、AIシミュレーター用のGameStateを構築
            GameState currentState = BuildCurrentGameState();

            MCTSSearchResult result = await mctsAlgorithm.Search(
                currentState,
                _thinkingTimeMilliseconds
            ).AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

            //// MCTS実行（メインスレッドをブロックしないよう、別スレッドで実行）
            //// WebGLはマルチスレッド非対応なので上のコードに変更
            //MCTSSearchResult result = await UniTask.RunOnThreadPool(() =>
            //{
            //    // MCTSに思考させる
            //    return mctsAlgorithm.Search(currentState, _thinkingTimeMilliseconds);
            //}, cancellationToken: this.GetCancellationTokenOnDestroy()); // オブジェクト破棄時（AI思考中にゲームを終了したとき）にキャンセル可能にする

            // ベンチマーク結果をログに出力
            UnityEngine.Debug.Log($"[MCTS Benchmark] Time: {result.ElapsedMilliseconds} ms");
            UnityEngine.Debug.Log($"[MCTS Benchmark] Sims: {result.TotalSimulations} simulations");
            UnityEngine.Debug.Log($"[MCTS Benchmark] Speed: {result.SimulationsPerSecond:F2} S/s");

            // 実際の手は .BestAction プロパティから取得
            GameAction bestAction = result.BestAction;

            // 思考が終わったことを通知
            Debug.Log("AI思考終了");
            _aiThinkingPublisher.Publish(new AIThinkingMessage(_aiColor, false));

            // メインスレッドで結果を反映
            if (!_isAITurn) return; // 思考中にゲームが終わったなど

            if (bestAction != null)
            {
                // 石を置くリクエストを発行
                _board.HideHighlight();
                _requestPutStonePublisher.Publish(new RequestPutStoneMessage(bestAction.Player, bestAction.Type, bestAction.Position));
            }
        }

        /// <summary>
        /// 現在のUnityの盤面(Board, PlayerInventory)から
        /// AIシミュレーター用のGameStateオブジェクトを構築する
        /// </summary>
        private GameState BuildCurrentGameState()
        {
            // シミュレーター用の状態をディープコピーして作成
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

            // 盤面（Cell[,]）をディープコピー
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
