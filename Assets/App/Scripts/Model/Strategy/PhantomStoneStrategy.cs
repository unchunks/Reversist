using System.Collections.Generic;

public class PhantomStoneStrategy : StoneStrategy
{
    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult outResult)
    {
        board.SetCell(move.Pos.x, move.Pos.y, StoneColor.None, StoneType.Normal);

        if (outResult == null) return;
        outResult.Effect.Type = StoneType.Phantom;
        outResult.Effect.Origin = move.Pos;
    }
}
