using System.Collections.Generic;

public class SpyStoneStrategy : StoneStrategy
{
    public override StoneColor GetAttackColor(StoneColor myColor)
        => (myColor == StoneColor.Black) ? StoneColor.White : StoneColor.Black;

    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult outResult)
    {
        // ”»’è‚Í‘Šè‚ÌF‚Ås‚¤‚ªAŒø‰Ê‚ÅŒ³‚É–ß‚é
        board.SetCell(move.Pos.x, move.Pos.y, move.PlayerColor, move.Type);

        if (outResult == null) return;
        outResult.Effect.Type = StoneType.Spy;
        outResult.Effect.Origin = move.Pos;
    }
}
