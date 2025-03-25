using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ReversiManager : MonoBehaviour
{
    [SerializeField] public GameObject blackPanel;
    [SerializeField] public GameObject whitePanel;

    public Dictionary<State, StoneSelectPanel> StoneSelectPanel;

    /// <summary>
    /// 現在のプレイヤーの色。どちらも置けないときはNone
    /// </summary>
    public State CurrentPlayer { get; private set; }
    public State Winner { get; private set; }
    public bool GameOver { get; private set; }
    public ObservableDictionary<State, StoneType> SelectedStoneType { get; set; }

    private Board _board;

    public void Awake()
    {
        StoneSelectPanel = new Dictionary<State, StoneSelectPanel>();
        StoneSelectPanel[State.Black] = blackPanel.GetComponent<StoneSelectPanel>();
        StoneSelectPanel[State.White] = whitePanel.GetComponent<StoneSelectPanel>();
    }

    public void Start()
    {
        _board = Board.GetInstance();

        CurrentPlayer = State.Black;

        SelectedStoneType = new ObservableDictionary<State, StoneType>
        {
            { State.Black, StoneType.Normal },
            { State.White, StoneType.Normal }
        };
        SelectedStoneType.OnValueChanged += (selectPlayer, selectStoneType) => 
        {
            if (CurrentPlayer == selectPlayer)
            {
                _board.UpdateAvailablePos(selectPlayer, selectStoneType);
            }
        };

        GameOver = false;

        Winner = State.None;
    }

    public bool IsAvailableStoneType()
    {
        if (StoneSelectPanel == null)
        {
            Debug.LogWarning("StoneSelectPanelがnullです");
        }
        if (SelectedStoneType == null)
        {
            Debug.LogWarning("SelectedStoneTypeがnullです");
        }

        return StoneSelectPanel[CurrentPlayer].AvailableStoneCount.GetCount(SelectedStoneType[CurrentPlayer]) > 0;
    }

    public void TwitchStone(Position boardPos)
    {
        Board.BoardStone[boardPos.Row, boardPos.Col].Twitch();
    }

    public State GetBoardState(Position boardPos)
    {
        return Board.BoardState[boardPos.Row, boardPos.Col];
    }

    public bool CanPut(Position boardPos)
    {
        return _board.CanPut(boardPos);
    }

    public IEnumerator MakeMove(State putPlayer, Position boardPos, StoneType stoneType)
    {
        yield return _board.MakeMove(putPlayer, boardPos, SelectedStoneType[putPlayer]);
    }

    public IEnumerator<Position> OccupiedPositions()
    {
        foreach (var position in _board.OccupiedPositions())
        {
            yield return position;
        }
    }

    /// <summary>
    /// ターンのパス処理を行う
    /// </summary>
    /// <returns>パス可能な場合はtrueを返す</returns>
    public bool PassTurn()
    {
        DecrementStoneCount(CurrentPlayer, SelectedStoneType[CurrentPlayer]);

        ChangePlayer();

        if (Board.AvailablePos.Count > 0)
        {
            return false;
        }
        // NormalやFrozenを選択していても、ReverseやDelayReverseで置ける場合はパスしない
        Dictionary< Position, List < Position >> availablePos = _board.GetAvailablePos(CurrentPlayer, StoneType.Reverse);
        if (availablePos.Count > 0 && StoneSelectPanel[CurrentPlayer].AvailableStoneCount.GetCount(StoneType.Reverse) > 0)
        {
            return true;
        }
        availablePos = _board.GetAvailablePos(CurrentPlayer, StoneType.DelayReverse);
        if (availablePos.Count > 0 && StoneSelectPanel[CurrentPlayer].AvailableStoneCount.GetCount(StoneType.DelayReverse) > 0)
        {
            return true;
        }

        ChangePlayer();

        if (Board.AvailablePos.Count == 0)
        {
            CurrentPlayer = State.None;
            GameOver = true;
            Winner = FindWinner();
        }
        return false;
    }



    private void ChangePlayer()
    {
        CurrentPlayer = CurrentPlayer.Opponent();
        _board.UpdateAvailablePos(CurrentPlayer, SelectedStoneType[CurrentPlayer]);
    }

    private State FindWinner()
    {
        if (Board.StoneCount[State.Black] > Board.StoneCount[State.White])
        {
            return State.Black;
        }
        else if (Board.StoneCount[State.Black] < Board.StoneCount[State.White])
        {
            return State.Black;
        }
        return State.None;
    }

    private void DecrementStoneCount(State putPlayer, StoneType stoneType)
    {
        StoneSelectPanel[putPlayer].AvailableStoneCount.RemoveCount(stoneType);
    }

    /*   UI用関数   */
    public void OnBlackSelectedToggle()
    {
        if (StoneSelectPanel == null)
        {
            Debug.LogWarning("StoneSelectPanelがnullです");
        }
        for (int i = 0; i < StoneSelectPanel[State.Black].ToggleComponents.Length; i++)
        {
            if (StoneSelectPanel[State.Black].ToggleComponents[i].isOn)
            {
                string selectedText = StoneSelectPanel[State.Black].LabelText[(StoneType)i].text;
                try
                {
                    SelectedStoneType[State.Black] = Enum.Parse<StoneType>(selectedText);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"stringからStoneTypeへの変換に失敗しました: {ex.Message}");
                }
                break;
            }
        }
    }
    public void OnWhiteSelectedToggle()
    {
        for (int i = 0; i < StoneSelectPanel[State.White].ToggleComponents.Length; i++)
        {
            if (StoneSelectPanel[State.White].ToggleComponents[i].isOn)
            {
                string selectedText = StoneSelectPanel[State.White].LabelText[(StoneType)i].text;
                try
                {
                    SelectedStoneType[State.White] = Enum.Parse<StoneType>(selectedText);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"stringからStoneTypeへの変換に失敗しました: {ex.Message}");
                }
                break;
            }
        }
    }
}
