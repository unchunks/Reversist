using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
    public enum GameMode
    {
        PVP,
        PVE
    }

    /// <summary>
    /// ゲームの進行、ターン管理、勝敗判定を行う
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _mainCam;

        [Inject] private Board _board;
        [Inject] private PlayerInventory _playerInventory;
        [Inject] private InputManager _inputManager;
        [Inject] private AIAgent _aiAgent;

        [Inject] private IPublisher<RequestPutStoneMessage> _requestPutStonePublisher;
        [Inject] private IPublisher<TurnChangedMessage> _turnChangedPublisher;
        [Inject] private IPublisher<GameOverMessage> _gameOverPublisher;
        [Inject] private IPublisher<GameStartMessage> _gameStartPublisher;
        [Inject] private ISubscriber<CellClickedMessage> _cellClickedSubscriber;
        [Inject] private ISubscriber<BoardInfo> _boardInfoSubscriber;
        [Inject] private ISubscriber<SelectedStoneTypeInfo> _selectedStoneTypeSubscriber;

        private GameMode _gameMode = ToReversiValues.GameMode;
        private StoneColor _aiColor = ToReversiValues.AiColor;
        
        private bool _isGameOver;
        private StoneColor _currentPlayer;
        private Dictionary<StoneColor, StoneType> _currentSelectedType;

        private void Start()
        {
            _isGameOver = false;
            _currentPlayer = StoneColor.Black;

            _currentSelectedType = new Dictionary<StoneColor, StoneType>
            {
                { StoneColor.Black, StoneType.Normal },
                { StoneColor.White, StoneType.Normal }
            };

            // メッセージへの登録
            _cellClickedSubscriber.Subscribe(OnCellClicked);
            _boardInfoSubscriber.Subscribe(OnBoardUpdated);
            _selectedStoneTypeSubscriber.Subscribe(OnSelectedStoneTypeChanged);

            // カメラ連動の登録
            _board.OnBoardSizeChanged = async (size) =>
            {
                await _mainCam.transform.DOMoveY(size, 1).SetEase(Ease.OutBounce).ToUniTask();
            };

            // AIの初期化を追加
            if (_gameMode == GameMode.PVE)
            {
                _aiAgent.Initialize(_aiColor);
            }

            // UIとハイライトの初期化
            _board.UpdateHighlight(_currentPlayer, _currentSelectedType[_currentPlayer]);
            _gameStartPublisher.Publish(new GameStartMessage(_gameMode, _aiColor));
            _turnChangedPublisher.Publish(new TurnChangedMessage(_currentPlayer));

            if (_gameMode == GameMode.PVE && _currentPlayer == _aiColor)
            {
                // AIのターン：プレイヤーの入力を無効化
                _inputManager.SetInputActive(false);
            }
            else
            {
                // 人間のターン：プレイヤーの入力を有効化
                _inputManager.SetInputActive(true);
            }
        }

        /// <summary>
        /// プレイヤーが使用する石の種類を変更した
        /// </summary>
        private void OnSelectedStoneTypeChanged(SelectedStoneTypeInfo info)
        {
            _currentSelectedType[info.Color] = info.Type;
            if (info.Color == _currentPlayer && !_isGameOver)
            {
                _board.UpdateHighlight(_currentPlayer, info.Type);
            }
        }

        /// <summary>
        /// ユーザーが盤面をクリックした
        /// </summary>
        private void OnCellClicked(CellClickedMessage msg)
        {
            if (_isGameOver) return;

            StoneType selectedType = _currentSelectedType[_currentPlayer];

            // 石の在庫をチェック
            if (!_playerInventory.HasStock(_currentPlayer, selectedType))
            {
                Debug.Log($"石の数が足りません: {_currentPlayer}, {selectedType}");
                return;
            }

            // 盤面に置けるかチェック
            if (!_board.CanPut(msg.Position))
            {
                Debug.Log($"その場所には置けません: {msg.Position}");
                return;
            }

            // 配置リクエストを発行
            _board.HideHighlight();
            _requestPutStonePublisher.Publish(new RequestPutStoneMessage(_currentPlayer, selectedType, msg.Position));
        }

        /// <summary>
        /// 盤面の更新が完了した（Boardが石を置いた）
        /// </summary>
        private async void OnBoardUpdated(BoardInfo info)
        {
            if (_isGameOver) return;

            // 石のアニメーション（特にExtend）とカメラワークが競合しないよう、
            // 少し待機してからターンチェックを行う
            await UniTask.DelayFrame(1);

            CheckNextTurn();
        }

        /// <summary>
        /// 次のターンに進み、パスやゲームオーバーを判定する
        /// </summary>
        private void CheckNextTurn()
        {
            _currentPlayer = _currentPlayer.Opponent();
            StoneType nextType = _currentSelectedType[_currentPlayer];

            if (_board.UpdateHighlight(_currentPlayer, nextType) == 0)
            {
                // パス
                _currentPlayer = _currentPlayer.Opponent();
                nextType = _currentSelectedType[_currentPlayer];
                Debug.Log(_currentPlayer.Opponent() + " がパスしました");

                if (_board.UpdateHighlight(_currentPlayer, nextType) == 0)
                {
                    // 両者置けない = ゲームオーバー
                    _isGameOver = true;
                    _board.HideHighlight();
                    _inputManager.SetInputActive(false);

                    StoneColor winColor = _board.GetWinColor();
                    int blackCount = _board.StoneCount[StoneColor.Black];
                    int whiteCount = _board.StoneCount[StoneColor.White];
                    _gameOverPublisher.Publish(new GameOverMessage(winColor, blackCount, whiteCount));
                    return;
                }
            }

            // ターンの交代を通知
            Debug.Log("次のターン: " + _currentPlayer);
            _turnChangedPublisher.Publish(new TurnChangedMessage(_currentPlayer));

            if (_gameMode == GameMode.PVE && _currentPlayer == _aiColor)
            {
                // AIのターン：プレイヤーの入力を無効化
                _inputManager.SetInputActive(false);
            }
            else
            {
                // 人間のターン：プレイヤーの入力を有効化
                _inputManager.SetInputActive(true);
            }
        }
    }
}