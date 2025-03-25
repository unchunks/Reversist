using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

namespace App.Reversi
{
    public class ReversiManager : MonoBehaviour
    {
        [SerializeField] private Camera _mainCam;
        [SerializeField] private LayerMask _hitLayer;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private Board _board;

        [Inject] private ISubscriber<SelectedStoneTypeInfo> _selectedStoneTypeInfoSubscriber;
        [Inject] private ISubscriber<BoardInfo> _boardInfoSubscriber;

        private bool _isGameOver;
        private StoneColor _currentPlayer;

        private Dictionary<StoneColor, AvailableStoneCount > _availableCount;

        private void Start()
        {
            _isGameOver = false;
            _currentPlayer = StoneColor.Black;

            // 盤上を初期配置に戻す

            // 各プレイヤーが持っている石を初期状態に戻す
            _availableCount = new Dictionary<StoneColor, AvailableStoneCount>();
            _availableCount[StoneColor.Black] = new AvailableStoneCount(new Dictionary<StoneType, int>
            {
                { StoneType.Normal, 61 },
                { StoneType.Extend, 1 },
                { StoneType.Frozen, 1 },
                { StoneType.Reverse, 5 },
                { StoneType.DelayReverse, 5 }
            });
            _availableCount[StoneColor.White] = new AvailableStoneCount(new Dictionary<StoneType, int>
            {
                { StoneType.Normal, 61 },
                { StoneType.Extend, 1 },
                { StoneType.Frozen, 1 },
                { StoneType.Reverse, 5 },
                { StoneType.DelayReverse, 5 }
            });

            // UIを初期状態に戻す
            foreach (var dic in _availableCount)
            {
                foreach (var count in dic.Value)
                {
                    _uiManager.UpdateAvailableCount(dic.Key, count.Key, count.Value);
                }
            }
            _uiManager.SetTopText(_currentPlayer);
            _uiManager.SetTotalStonesCountText(_board.StoneCount);

            // Subscribeの登録
            _selectedStoneTypeInfoSubscriber.Subscribe( (info) =>
            {
                if (info.Color == _currentPlayer)
                {
                    _board.UpdateHighlight(info.Color, info.Type);
                }
            });
            _boardInfoSubscriber.Subscribe( (info) =>
            {
                _uiManager.UpdateUI(info);
            });

            _board.OnBoardSizeChanged = async (size) =>
            {
                await _mainCam.transform.DOMoveY(size, 1).ToUniTask();
            };
            _board.UpdateHighlight(_currentPlayer, _uiManager.GetSelectedStoneType(_currentPlayer));
        }

        private async void Update()
        {
            if (_isGameOver)
            {
                await _uiManager.ShowResultView();
                _isGameOver = false;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);

                // 置ける場所に入力があったら
                if (Physics.Raycast(ray, out RaycastHit hitInfo, 20f, _hitLayer))
                {
                    Vector3 impact = new Vector3(hitInfo.point.z, hitInfo.point.y, hitInfo.point.x);
                    Position boardPos = SceneToBoardPos(impact);

                    Debug.Log($"{_currentPlayer}, {_uiManager.GetSelectedStoneType(_currentPlayer)}");

                    // 石の数が足りるかチェック
                    if (!DecreaseSelectedType(_currentPlayer))
                    {
                        Debug.Log($"石の数が足りません");
                        return;
                    }

                    StoneType putType = _uiManager.GetSelectedStoneType(_currentPlayer);

                    _board.HideHighlight();

                    // 石を置く
                    await _board.PutProcess(_currentPlayer, putType, boardPos);

                    // 次のプレイヤーの盤にして置ける場所を更新
                    _currentPlayer = _currentPlayer.Opponent();
                    // 置ける場所がなければパスして次のプレイヤーに
                    if(_board.UpdateHighlight(_currentPlayer, _uiManager.GetSelectedStoneType(_currentPlayer)) == 0)
                    {
                        _currentPlayer = _currentPlayer.Opponent();
                        if (_board.UpdateHighlight(_currentPlayer, _uiManager.GetSelectedStoneType(_currentPlayer)) == 0)
                        {
                            _isGameOver = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 選択中のタイプの石を1個消費する
        /// </summary>
        /// <param name="putPlayer"></param>
        /// <returns>もともと残りが0ならfalseを返す</returns>
        private bool DecreaseSelectedType(StoneColor putPlayer)
        {
            StoneType selectedType = _uiManager.GetSelectedStoneType(putPlayer);
            bool couldDecrease = _availableCount[putPlayer].Decrease(selectedType);
            if (couldDecrease)
            {
                int count = _availableCount[putPlayer].AvailableCount[selectedType];
                _uiManager.UpdateAvailableCount(putPlayer, selectedType, count);
            }
            return couldDecrease;
        }

        private Position SceneToBoardPos(Vector3 scenePos)
        {
            int col = (int)(scenePos.x + 0.5f);
            int row = (int)(scenePos.z + 0.5f);
            return new Position(col, row);
        }
    }
}
