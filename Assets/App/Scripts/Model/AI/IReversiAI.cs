using System.Collections.Generic;

public interface IReversiAI
{
    PlayerMove CalculateNextMove(BoardState currentBoard, StoneColor myColor, StoneInventory inventory);
}
