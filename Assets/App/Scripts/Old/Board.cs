using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class Board : MonoBehaviour
{
    private static Board _instance;

    public readonly static int MAX_ROW = 12;
    public readonly static int MAX_COL = 12;
    public readonly static int DEF_ROW = 8;
    public readonly static int DEF_COL = 8;
    public static int CurrentRow { get; private set; } = DEF_ROW;
    public static int CurrentCol { get; private set; } = DEF_COL;

    [SerializeField] private Stone stoneBlack;
    [SerializeField] private Stone stoneWhite;
    private static Dictionary<State, Stone> stonePrefabs = new Dictionary<State, Stone>();

    [SerializeField] private GameObject highlightPrefab;
    private static List<GameObject> highlights = new List<GameObject>();

    /// <summary>
    /// 左下が原点
    /// </summary>
    public static State[,] BoardState;
    public static Stone[,] BoardStone;
    public static Dictionary<State, int> StoneCount { get; private set; }

    /// <summary>
    /// 置ける場所と置いたときひっくり返す場所のリスト
    /// </summary>
    public static Dictionary<Position, List<Position>> AvailablePos { get; private set; }

    private Dictionary<State, Position> delayFlipPos = new Dictionary<State, Position>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;

            stonePrefabs[State.Black] = stoneBlack;
            stonePrefabs[State.White] = stoneWhite;
            Init();
        }
    }

    public static Board GetInstance()
    {
        return _instance;
    }

    public void Init()
    {
        StartCoroutine(Initialize());
    }


    public IEnumerator Initialize()
    {
        BoardState = new State[CurrentRow + 2, CurrentCol + 2];
        BoardStone = new Stone[CurrentRow + 2, CurrentCol + 2];

        for (int row = 0; row < CurrentRow + 2; row++)
        {
            for (int col = 0; col < CurrentCol + 2; col++)
            {
                BoardState[row, col] = State.None;
            }
        }

        Put(State.Black, new Position(4, 4), StoneType.Normal);
        Put(State.Black, new Position(5, 5), StoneType.Normal);
        Put(State.White, new Position(4, 5), StoneType.Normal);
        Put(State.White, new Position(5, 4), StoneType.Normal);

        // Reverseの長所紹介用配置
        //Put(State.Black, new Position(8, 4), StoneType.Normal); // 黒
        //Put(State.White, new Position(7, 4), StoneType.Normal); // 白
        //Put(State.White, new Position(6, 4), StoneType.Normal); // 白
        //Put(State.White, new Position(5, 4), StoneType.Normal); // 白
        //Put(State.Black, new Position(4, 4), StoneType.Normal); // 黒
        //Put(State.Black, new Position(3, 4), StoneType.Normal); // 黒
        //Put(State.Black, new Position(2, 4), StoneType.Normal); // 黒
        //                                                        // 空きマス

        // DelayReverseの長所紹介用配置
        //Put(State.Black, new Position(8, 4), StoneType.Normal); // 黒
        //Put(State.White, new Position(7, 4), StoneType.Normal); // 白
        //Put(State.Black, new Position(6, 4), StoneType.Normal); // 黒
        //                                                        // 空きマス
        //                                                        // 空きマス
        //Put(State.Black, new Position(3, 4), StoneType.Normal); // 黒
        //Put(State.White, new Position(2, 4), StoneType.Normal); // 白
        //Put(State.Black, new Position(1, 4), StoneType.Normal); // 黒

        // Frozenの長所紹介用配置
        //                                                        // 空きマス
        //Put(State.White, new Position(7, 4), StoneType.Normal); // 白
        //Put(State.Black, new Position(6, 4), StoneType.Normal); // 黒
        //Put(State.White, new Position(5, 4), StoneType.Normal); // 白
        //Put(State.White, new Position(4, 4), StoneType.Normal); // 白
        //Put(State.White, new Position(3, 4), StoneType.Normal); // 白
        //                                                        // 空きマス
        //                                                        // 空きマス

        yield return new WaitForSeconds(0.30f);

        StoneCount = new Dictionary<State, int>()
        {
            { State.Black, 2 },
            { State.White, 2 },
        };

        delayFlipPos[State.Black] = null;
        delayFlipPos[State.White] = null;

        UpdateAvailablePos(State.Black);

        ShowHighlights();
    }

    /// <summary>
    /// ハイライトを削除、石を置く、ひっくり返す、石の数を更新、ハイライトを表示の順に処理する
    /// </summary>
    /// <param name="putPlayer"></param>
    /// <param name="putPos"></param>
    /// <param name="stoneType"></param>
    public IEnumerator MakeMove(State putPlayer, Position putPos, StoneType stoneType)
    {
        if (CanPut(putPos))
        {
            RemoveHighlights();

            // ReverseとDelayReverseの場合は一度逆向きに置く
            if (ReverseType(stoneType))
            {
                List<Position> flipPositions = FindFlipPos(putPos, putPlayer.Opponent());
                Put(putPlayer, putPos, stoneType);
                yield return new WaitForSeconds(0.30f);

                Flip(flipPositions);
                yield return new WaitForSeconds(0.50f);

                UpdateStoneCounts(putPlayer.Opponent(), flipPositions.Count);

                if (stoneType == StoneType.Reverse)
                {
                    flipPositions = FindFlipPos(putPos, putPlayer);
                    Flip(putPos);
                    yield return new WaitForSeconds(0.50f);

                    Flip(flipPositions);
                    yield return new WaitForSeconds(0.50f);

                    UpdateStoneCounts(putPlayer, flipPositions.Count);
                }
            }
            else
            {
                Put(putPlayer, putPos, stoneType);
                yield return new WaitForSeconds(0.30f);

                Flip(AvailablePos[putPos]);
                yield return new WaitForSeconds(0.50f);

                UpdateStoneCounts(putPlayer, AvailablePos[putPos].Count);
            }


            // DelayReverseの場合
            // 次に相手がが置いた後にひっくり返す
            Position flipPos = delayFlipPos[putPlayer.Opponent()];
            if (flipPos != null && flipPos != putPos)
            {
                Flip(flipPos);
                yield return new WaitForSeconds(0.50f);

                // ひっくり返した後の色を参照するため、ここでStateを取得
                State posState = BoardState[flipPos.Row, flipPos.Col];
                List<Position> flipPositions = FindFlipPos(flipPos, posState);
                Flip(flipPositions);
                yield return new WaitForSeconds(0.50f);

                delayFlipPos[putPlayer.Opponent()] = null;
                UpdateStoneCounts(putPlayer, flipPositions.Count);
            }

            ShowHighlights();
        }
    }

    public void Put(State putPlayer, Position putPos, StoneType stoneType)
    {
        State putState = putPlayer;
        if (ReverseType(stoneType))
        {
            putState = putPlayer.Opponent();
        }
        BoardState[putPos.Row, putPos.Col] = putState;
        BoardStone[putPos.Row, putPos.Col] = Instantiate(stonePrefabs[putState], BoardToScenePos(putPos), Quaternion.identity);
        BoardStone[putPos.Row, putPos.Col].Init(putState, stoneType);
        if (stoneType == StoneType.DelayReverse)
        {
            delayFlipPos[putPlayer] = putPos;
        }
    }

    public void Remove(Position removePos)
    {

    }

    public void Flip(List<Position> flipPositions)
    {
        foreach (Position pos in flipPositions)
        {
            Flip(pos);
        }
    }

    public void Flip(Position flipPos)
    {
        if (BoardStone[flipPos.Row, flipPos.Col].Type == StoneType.Frozen)
        {
            return;
        }
        BoardState[flipPos.Row, flipPos.Col] = BoardState[flipPos.Row, flipPos.Col].Opponent();
        BoardStone[flipPos.Row, flipPos.Col].Flip();
    }

    public bool CanPut(Position putPos)
    {
        return AvailablePos.ContainsKey(putPos);
    }

    /// <summary>
    /// 石が置かれている位置を一つずつ返す
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Position> OccupiedPositions()
    {
        for (int row = 1; row <= CurrentRow; row++)
        {
            for (int col = 1; col <= CurrentCol; col++)
            {
                if (BoardState[row, col] != State.None)
                {
                    yield return new Position(row, col);
                }
            }
        }
    }

    public Dictionary<Position, List<Position>> GetAvailablePos(State putPlayer, StoneType selectedStoneType = StoneType.Normal)
    {
        Dictionary<Position, List<Position>> availablePos = new Dictionary<Position, List<Position>>();
        for (int row = 1; row <= CurrentRow; row++)
        {
            for (int col = 1; col <= CurrentCol; col++)
            {
                Position putPos = new Position(row, col);

                if (BoardState[putPos.Row, putPos.Col] != State.None)
                {
                    continue;
                }

                if (IsAvailablePos(putPos, putPlayer, out List<Position> pos))
                {
                    availablePos[putPos] = pos;
                }
                else if (ReverseType(selectedStoneType) && IsAvailablePos(putPos, putPlayer.Opponent(), out List<Position> posOpponent))
                {
                    availablePos[putPos] = posOpponent;
                }
            }
        }
        return availablePos;
    }
    public void UpdateAvailablePos(State putPlayer, StoneType selectedStoneType = StoneType.Normal)
    {
        Debug.Log("UpdateAvailablePos");
        RemoveHighlights();
        AvailablePos = GetAvailablePos(putPlayer, selectedStoneType);
        ShowHighlights();
    }

    public void UpdateHighlight()
    {
        RemoveHighlights();
        ShowHighlights();
    }



    private bool ReverseType(StoneType type)
    {
        return type == StoneType.Reverse || type == StoneType.DelayReverse;
    }

    private Position SceneToBoardPos(Vector3 scenePos)
    {
        int col = (int)(scenePos.x + 0.5f);
        int row = (int)(scenePos.z + 0.5f);
        return new Position(col, row);
    }

    private Vector3 BoardToScenePos(Position pos)
    {
        return BoardToScenePos(pos.Row, pos.Col);
    }
    private Vector3 BoardToScenePos(int row, int col)
    {
        return new Vector3(col, 0, row);
    }

    private void ShowHighlights()
    {
        foreach (Position boardPos in AvailablePos.Keys)
        {
            Vector3 scenePos = BoardToScenePos(boardPos);
            GameObject highlight = Instantiate(highlightPrefab, scenePos, Quaternion.identity);
            highlights.Add(highlight);
        }
    }

    private void RemoveHighlights()
    {
        highlights.ForEach(Destroy);
        highlights.Clear();
    }

    private bool IsInsideBoard(int row, int col)
    {
        return row > 0 && row <= CurrentRow && col > 0 && col <= CurrentCol;
    }

    private void UpdateStoneCounts(State putPlayer, int flipCount)
    {
        StoneCount[putPlayer] += flipCount + 1;
        StoneCount[putPlayer.Opponent()] -= flipCount;
    }

    private List<Position> FindFlipPosInDir(Position pos, State playerColor, int rDelta, int cDelta)
    {
        List<Position> availablePos = new List<Position>();
        int row = pos.Row + rDelta;
        int col = pos.Col + cDelta;

        while (IsInsideBoard(row, col) && BoardState[row, col] != State.None)
        {
            if (BoardState[row, col] == playerColor.Opponent())
            {
                availablePos.Add(new Position(row, col));
            }
            else if (BoardState[row, col] == playerColor)
            {
                return availablePos;
            }

            row += rDelta;
            col += cDelta;
        }

        return new List<Position>();
    }

    private List<Position> FindFlipPos(Position pos, State playerColor)
    {
        List<Position> availablePos = new List<Position>();

        for (int rDelta = -1; rDelta <= 1; rDelta++)
        {
            for (int cDelta = -1; cDelta <= 1; cDelta++)
            {
                if (rDelta == 0 && cDelta == 0)
                {
                    continue;
                }
                availablePos.AddRange(FindFlipPosInDir(pos, playerColor, rDelta, cDelta));
            }
        }

        return availablePos;
    }

    private bool IsAvailablePos(Position pos, State playerColor, out List<Position> flipPos)
    {
        if (BoardState[pos.Row, pos.Col] != State.None)
        {
            flipPos = new List<Position>();
            return false;
        }

        flipPos = FindFlipPos(pos, playerColor);
        return flipPos.Count > 0;
    }

    private IEnumerator AddStartStones()
    {
        foreach (Position boardPos in OccupiedPositions())
        {
            State player = BoardState[boardPos.Row, boardPos.Col];
            Put(player, boardPos, StoneType.Normal);
            yield return new WaitForSeconds(0.30f);
        }
    }

}
