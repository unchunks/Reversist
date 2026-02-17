using System.Collections.Generic;

public class ExpanderStoneStrategy : StoneStrategy
{
    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult outResult)
    {
        board.ExpandBoard();

        // ˜^
        if (outResult == null) return;
        outResult.Effect.Type = StoneType.Expander;
        outResult.Effect.Origin = move.Pos;
        outResult.WasBoardExpanded = true;
    }
}
