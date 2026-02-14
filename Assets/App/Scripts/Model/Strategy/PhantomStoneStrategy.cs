using System.Collections.Generic;

public class PhantomStoneStrategy : StoneStrategy
{
    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult result)
    {
        board.SetCell(move.Pos.x, move.Pos.y, StoneColor.None, StoneType.Normal);

        result.Effects.Add(new EffectEvent { Type = StoneType.Phantom, Origin = move.Pos });
    }
}
