using App.Reversi.AI;
using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace App.Reversi
{
    public class Board : MonoBehaviour
    {
        public const int MAX_BOARD_SIZE = 12;
        public const int DEF_BOARD_SIZE = 8;
        public const int DELAY_COUNT = 1;

        [SerializeField] private Cell _cellPrefab;

        [Inject] private IPublisher<BoardInfo> _boardInfoPublisher;
        [Inject] private IPublisher<PlaySoundEffectMessage> _soundPublisher;
        [Inject] private IPublisher<PlayVFXMessage> _vfxPublisher;

        [Inject] private ISubscriber<RequestPutStoneMessage> _requestSubscriber;

        [Inject] private IObjectResolver _resolver;

        public Func<int, UniTask> OnBoardSizeChanged;
        public Dictionary<StoneColor, int> StoneCount;

        public Cell[,] BoardCells { get; private set; }
        public int CurrentBoardSize { get; private set; }
        public List<ReverseCountDown> DelayReverseStack { get; private set; }

        private HashSet<Position> _availableMoves = new HashSet<Position>();

        public static Position[] GetCorners(int boardSize)
        {
            Position[] corner = new Position[4];
            int diff = (MAX_BOARD_SIZE - boardSize) / 2;
            corner[0] = new Position(diff, diff);
            corner[1] = new Position(diff, MAX_BOARD_SIZE - diff - 1);
            corner[2] = new Position(MAX_BOARD_SIZE - diff - 1, diff);
            corner[3] = new Position(MAX_BOARD_SIZE - diff - 1, MAX_BOARD_SIZE - diff - 1);
            return corner;
        }


        private void Awake()
        {
            _requestSubscriber.Subscribe(OnPutRequest);

            BoardCells = new Cell[MAX_BOARD_SIZE, MAX_BOARD_SIZE];
            for (int row = 0; row < MAX_BOARD_SIZE; row++)
            {
                for (int col = 0; col < MAX_BOARD_SIZE; col++)
                {
                    BoardCells[row, col] = _resolver.Instantiate(_cellPrefab, new Vector3(col, 0, row), Quaternion.identity);
                }
            }

            StoneCount = new Dictionary<StoneColor, int>
            {
                { StoneColor.Black, 0 },
                { StoneColor.White, 0 }
            };
            _ = Put(StoneColor.Black, StoneType.Normal, new Position(5, 5));
            _ = Put(StoneColor.Black, StoneType.Normal, new Position(6, 6));
            _ = Put(StoneColor.White, StoneType.Normal, new Position(6, 5));
            _ = Put(StoneColor.White, StoneType.Normal, new Position(5, 6));

            CurrentBoardSize = DEF_BOARD_SIZE;
            DelayReverseStack = new List<ReverseCountDown>();
        }

        private void OnPutRequest(RequestPutStoneMessage msg)
        {
            PutProcess(msg.Player, msg.Type, msg.Position).Forget();
        }

        public async UniTask PutProcess(StoneColor putPlayer, StoneType putType, Position clickPos)
        {
            StoneColor putColor = putPlayer;
            if (putType.IsReverseType())
            {
                putColor = putPlayer.Opponent();
            }

            List<Position> reversePos = FindReversePos(putColor, clickPos);

            if (BoardCells[clickPos.Row, clickPos.Col].Color != StoneColor.None)
            {
                Debug.LogWarning("既に石が置かれているマスが指定されました。");
                return;
            }

            await Put(putColor, putType, clickPos);
            await Flip(reversePos);

            // 遅延反転処理
            for (int i = 0; i < DelayReverseStack.Count; i++)
            {
                DelayReverseStack[i].Delay--;
                if (DelayReverseStack[i].Delay <= 0)
                {
                    Position pos = DelayReverseStack[i].Pos;
                    StoneColor afterColor = BoardCells[pos.Row, pos.Col].Color.Opponent();
                    reversePos = FindReversePos(afterColor, pos);
                    _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.Reverse));
                    _vfxPublisher.Publish(new PlayVFXMessage(VFXType.Reverse, BoardCells[pos.Row, pos.Col].transform.position));
                    await Flip(pos);
                    await Flip(reversePos);
                    DelayReverseStack.RemoveAt(i);
                }
            }

            // 特殊石処理
            switch (putType)
            {
                case StoneType.Extend:
                    _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.Extend));
                    _vfxPublisher.Publish(new PlayVFXMessage(VFXType.Extend, BoardCells[clickPos.Row, clickPos.Col].transform.position));
                    CurrentBoardSize = Math.Min(CurrentBoardSize + 2, MAX_BOARD_SIZE);
                    float size = (float)(CurrentBoardSize * 0.1 + 0.004);
                    await UniTask.WhenAll(
                        transform.DOScale(new Vector3(size, 1, size), 1).ToUniTask()
                    );
                    break;

                case StoneType.Reverse:
                    _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.Reverse));
                    _vfxPublisher.Publish(new PlayVFXMessage(VFXType.Reverse, BoardCells[clickPos.Row, clickPos.Col].transform.position));
                    StoneColor afterColor = BoardCells[clickPos.Row, clickPos.Col].Color.Opponent();
                    reversePos = FindReversePos(afterColor, clickPos);
                    await Flip(clickPos);
                    await Flip(reversePos);
                    break;

                case StoneType.DelayReverse:
                    DelayReverseStack.Add(new ReverseCountDown(clickPos, DELAY_COUNT));
                    break;
            }

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
            await BoardCells[putPos.Row, putPos.Col].Put(putColor, putType);
        }

        private async UniTask Flip(Position pos)
        {
            StoneCount[BoardCells[pos.Row, pos.Col].Color]--;
            StoneCount[BoardCells[pos.Row, pos.Col].Color.Opponent()]++;
            await BoardCells[pos.Row, pos.Col].Flip();
        }

        private async UniTask Flip(List<Position> posList)
        {
            await UniTask.WhenAll(posList.Select(pos => Flip(pos)));
        }

        public void HideHighlight()
        {
            foreach (var cell in BoardCells)
            {
                cell.SetHighlight(false);
            }
        }

        public List<Position> GetAvailableMoves()
        {
            return _availableMoves.ToList();
        }

        public int UpdateHighlight(StoneColor nextColor, StoneType nextType)
        {
            int availableCount = 0;
            _availableMoves.Clear();

            foreach (var cell in BoardCells)
            {
                if (!IsInBoard(cell.Row, cell.Col)) continue;
                if (cell.isPlased) continue;

                Position checkPos = new Position(cell.Row, cell.Col);
                List<Position> reversePos = FindReversePos(nextColor, checkPos);
                if (reversePos.Count > 0)
                {
                    cell.SetHighlight(true);
                    _availableMoves.Add(checkPos);
                    availableCount++;
                    continue;
                }
                if (nextType.IsReverseType())
                {
                    reversePos = FindReversePos(nextColor.Opponent(), checkPos);
                    if (reversePos.Count > 0)
                    {
                        cell.SetHighlight(true);
                        _availableMoves.Add(checkPos);
                        availableCount++;
                        continue;
                    }
                }
                cell.SetHighlight(false);
            }
            return availableCount;
        }

        public bool CanPut(Position pos)
        {
            return _availableMoves.Contains(pos);
        }

        public StoneColor GetWinColor()
        {
            if (StoneCount[StoneColor.Black] > StoneCount[StoneColor.White]) return StoneColor.Black;
            else if (StoneCount[StoneColor.Black] < StoneCount[StoneColor.White]) return StoneColor.White;
            else return StoneColor.None;
        }

        private List<Position> FindReversePos(StoneColor putColor, Position putPos)
        {
            List<Position> dir = new List<Position> {
                new Position(-1, -1), new Position(-1, 0), new Position(-1, 1),
                new Position(0, -1), new Position(0, 1),
                new Position(1, -1), new Position(1, 0), new Position(1, 1)
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
                if (BoardCells[currentRow, currentCol].Color == StoneColor.None) break;
                if (BoardCells[currentRow, currentCol].Color == putColor) return reversePos;
                reversePos.Add(new Position(currentRow, currentCol));
                currentRow += dRow;
                currentCol += dCol;
            }
            return new List<Position>();
        }

        private bool IsInBoard(int row, int col)
        {
            int center = MAX_BOARD_SIZE / 2;
            int halfSize = CurrentBoardSize / 2;
            return row >= center - halfSize && row < center + halfSize && col >= center - halfSize && col < center + halfSize;
        }
    }
}
