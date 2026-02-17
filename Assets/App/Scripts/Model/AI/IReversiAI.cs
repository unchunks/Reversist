using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime;

public interface IReversiAI
{
    PlayerMove CalculateNextMove(BoardState currentBoard, StoneColor myColor, StoneInventory inventory, CancellationToken token);
}
