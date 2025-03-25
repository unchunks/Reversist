using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;

namespace App.Reversi
{
    public class Board : MonoBehaviour
    {
        public readonly int MAX_BOARD_SIZE = 12;
        public readonly int DEF_BOARD_SIZE = 8;
        public readonly int DELAY_COUNT = 1;

        [SerializeField] private Cell _cellPrefab;

        [Inject] private IPublisher<BoardInfo> _boardInfoPublisher;

        public Func<int, UniTask> OnBoardSizeChanged;

        /// <summary> 両者の盤上の石の数 </summary>
        public Dictionary<StoneColor, int> StoneCount;

        /// <summary> 盤の各セルの状態 </summary>
        private Cell[,] _boardCell;        
        /// <summary> 現在のボードのサイズ </summary>
        private int _currentBoardSize;
        /// <summary> 数ターン後にひっくり返る石のスタック </summary>
        private List<ReverseCountDown> _delayReverseStack;

        private void Start()
        {
            // 盤の初期化
            _boardCell = new Cell[MAX_BOARD_SIZE, MAX_BOARD_SIZE];
            for (int row = 0; row < MAX_BOARD_SIZE; row++)
            {
                for (int col = 0; col < MAX_BOARD_SIZE; col++)
                {
                    _boardCell[row, col] = Instantiate(_cellPrefab, new Vector3(col, 0, row), Quaternion.identity);
                }
            }

            // 初期配置
            StoneCount = new Dictionary<StoneColor, int>
            {
                { StoneColor.Black, 0 },
                { StoneColor.White, 0 }
            };
            _ = Put(StoneColor.Black, StoneType.Normal, new Position(5, 5));
            _ = Put(StoneColor.Black, StoneType.Normal, new Position(6, 6));
            _ = Put(StoneColor.White, StoneType.Normal, new Position(6, 5));
            _ = Put(StoneColor.White, StoneType.Normal, new Position(5, 6));

            // 他変数の初期化
            _currentBoardSize = DEF_BOARD_SIZE;
            _delayReverseStack = new List<ReverseCountDown>();
        }

        /// <summary>
        /// 盤を押したときの一連の処理を行う
        /// </summary>
        /// <param name="putPlayer">誰が</param>
        /// <param name="putType">何を</param>
        /// <param name="clickPos">どこに置いた</param>
        /// <returns> 置けなかった場合nullを返す </returns>
        public async UniTask PutProcess(StoneColor putPlayer, StoneType putType, Position clickPos)
        {
            // はじめ逆の色で置くタイプの石を処理
            StoneColor putColor = putPlayer;
            if (putType.IsReverseType())
            {
                putColor = putPlayer.Opponent();
            }
            
            List<Position> reversePos = FindReversePos(putColor, clickPos);

            // 埋まっているマス・ひっくり返す場所がない場合は、置けない
            if (_boardCell[clickPos.Row, clickPos.Col].Color != StoneColor.None)// || reversePos.Count == 0)
            {
                return;
            }

            await Put(putColor, putType, clickPos);
            await Reverse(reversePos);

            // 遅延反転の処理
            for (int i = 0; i < _delayReverseStack.Count; i++)
            {
                _delayReverseStack[i].Delay--;
                if (_delayReverseStack[i].Delay <= 0)
                {
                    Position pos = _delayReverseStack[i].Pos;
                    StoneColor afterColor = _boardCell[pos.Row, pos.Col].Color.Opponent();
                    reversePos = FindReversePos(afterColor, pos);
                    await Reverse(pos);
                    await Reverse(reversePos);
                    _delayReverseStack.RemoveAt(i);
                }
            }

            // 特殊石の処理
            switch (putType)
            {
                case StoneType.Extend:
                    _currentBoardSize = Math.Min(_currentBoardSize + 2, MAX_BOARD_SIZE);
                    float size = (float)(_currentBoardSize * 0.1 + 0.004);
                    await UniTask.WhenAll(
                        transform.DOScale(new Vector3(size, 1, size), 1).ToUniTask(),
                        OnBoardSizeChanged.Invoke(_currentBoardSize)
                    );
                    break;
                case StoneType.Reverse:
                    StoneColor afterColor = _boardCell[clickPos.Row, clickPos.Col].Color.Opponent();
                    reversePos = FindReversePos(afterColor, clickPos);
                    await Reverse(clickPos);
                    await Reverse(reversePos);
                    break;

                case StoneType.DelayReverse:
                    _delayReverseStack.Add(new ReverseCountDown(clickPos, DELAY_COUNT));
                    break;
            }

            // UI更新のために情報を送信
            _boardInfoPublisher.Publish(new BoardInfo(putPlayer, putColor, putType, reversePos, StoneCount));
        }

        private async UniTask Put(StoneColor putColor, StoneType putType, Position putPos)
        {
            if (putType == StoneType.None)
            {
                Debug.LogError("putTypeがNoneになっています");
                return;
            }

            StoneCount[putColor]++;
            await _boardCell[putPos.Row, putPos.Col].Put(putColor, putType);
        }

        private async UniTask Reverse(Position pos)
        {
            StoneCount[_boardCell[pos.Row, pos.Col].Color]--;
            StoneCount[_boardCell[pos.Row, pos.Col].Color.Opponent()]++;
            await _boardCell[pos.Row, pos.Col].Reverse();
        }

        private async UniTask Reverse(List<Position> posList)
        {
            await UniTask.WhenAll(posList.Select(pos => Reverse(pos)));
        }

        /// <summary>
        /// 全てのハイライトをオフにする
        /// </summary>
        public void HideHighlight()
        {
            foreach (var cell in _boardCell)
            {
                cell.SetHighlight(false);
            }
        }

        /// <summary>
        /// ハイライトのオンとオフを行う
        /// </summary>
        /// <param name="nextColor"></param>
        /// <param name="nextType"></param>
        /// <returns>ハイライトの数</returns>
        public int UpdateHighlight(StoneColor nextColor, StoneType nextType)
        {
            int availableCount = 0;
            foreach (var cell in _boardCell)
            {
                if (!IsInBoard(cell.Row, cell.Col))
                {
                    continue;
                }

                if (cell.isPlased)
                {
                    continue;
                }

                Position checkPos = new Position(cell.Row, cell.Col);
                List<Position> reversePos = FindReversePos(nextColor, checkPos);
                if (reversePos.Count > 0)
                {
                    cell.SetHighlight(true);
                    availableCount++;
                    continue;
                }
                if (nextType.IsReverseType())
                {
                    reversePos = FindReversePos(nextColor.Opponent(), checkPos);

                    if (reversePos.Count > 0)
                    {
                        cell.SetHighlight(true);
                        availableCount++;
                        continue;
                    }
                }
                cell.SetHighlight(false);
            }
            return availableCount;
        }

        private List<Position> FindReversePos(StoneColor putColor, Position putPos)
        {
            List<Position> dir = new List<Position> {
                new Position(-1, -1),
                new Position(-1, 0),
                new Position(-1, 1),
                new Position(0, -1),
                new Position(0, 1),
                new Position(1, -1),
                new Position(1, 0),
                new Position(1, 1)
            };

            var reversePos = new List<Position>();
            foreach (var d in dir)
            {
                reversePos.AddRange(FindReversePosInDir(putColor, putPos, d.Row, d.Col));
            }
            return reversePos;
        }

        private List<Position> FindReversePosInDir(StoneColor putColor, Position putPos, int dRow, int dCol)
        {
            var reversePos = new List<Position>();
            var currentRow = putPos.Row + dRow;
            var currentCol = putPos.Col + dCol;
            while (IsInBoard(currentRow, currentCol))
            {
                if (_boardCell[currentRow, currentCol].Color == StoneColor.None)
                {
                    break;
                }
                if (_boardCell[currentRow, currentCol].Color == putColor)
                {
                    return reversePos;
                }
                reversePos.Add(new Position(currentRow, currentCol));
                currentRow += dRow;
                currentCol += dCol;
            }
            return new List<Position>();
        }

        private bool IsInBoard(int row, int col)
        {
            int center = MAX_BOARD_SIZE / 2;
            int halfSize = _currentBoardSize / 2;
            return row >= center - halfSize && row < center + halfSize && col >= center - halfSize && col < center + halfSize;
        }
    }
}