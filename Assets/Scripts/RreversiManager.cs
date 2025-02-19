using System;
using System.Collections.Generic;
using UnityEngine;

public class ReversiManager
{
    /// <summary>
    /// 現在のプレイヤーの色。どちらも置けないときはNone
    /// </summary>
    public State CurrentPlayer { get; private set; }
    public State Winner { get; private set; }
    public bool GameOver { get; private set; }

    // TODO: 白と黒で石の残機を別で管理する
    // public StoneCountManager StoneCountManager { get; private set; }
    public StoneCount BlackStoneCount { get; private set; }
    public StoneCount WhiteStoneCount { get; private set; }

    private StoneType _selectedStoneType;
    public StoneType SelectedStoneType
    {
        get => _selectedStoneType;
        set
        {
            Debug.Log($"SelectedStoneType: {_selectedStoneType}");
            if (_selectedStoneType == value) 
            {
                return;
            }

            _selectedStoneType = value;
            var board = Board.GetInstance();
            if (board == null) return;

            board.UpdateAvailablePos(CurrentPlayer, SelectedStoneType);
        }
    }

    public ReversiManager()
    {
        Init();
    }

    public void Init()
    {
        CurrentPlayer = State.Black;

        BlackStoneCount = new StoneCount();
        WhiteStoneCount = new StoneCount();

        SelectedStoneType = StoneType.Normal;

        GameOver = false;

        Winner = State.None;
    }

    public void PassTurn()
    {
        DecrementStoneCount(CurrentPlayer, SelectedStoneType);

        ChangePlayer();

        if (Board.AvailablePos.Count > 0)
        {
            return;
        }

        ChangePlayer();

        if (Board.AvailablePos.Count == 0)
        {
            CurrentPlayer = State.None;
            GameOver = true;
            Winner = FindWinner();
        }
    }

    private void ChangePlayer()
    {
        CurrentPlayer = CurrentPlayer.Opponent();
        Board.GetInstance().UpdateAvailablePos(CurrentPlayer, SelectedStoneType);
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
        if (putPlayer == State.Black)
        {
            BlackStoneCount.RemoveCount(stoneType);
        }
        else if (putPlayer == State.White)
        {
            WhiteStoneCount.RemoveCount(stoneType);
        }
    }
}
