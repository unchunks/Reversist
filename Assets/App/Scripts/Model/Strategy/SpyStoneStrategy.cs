using System.Collections.Generic;

public class SpyStoneStrategy : StoneStrategy
{
    public override StoneColor GetAttackColor(StoneColor myColor)
    {
        // ”»’è‚Í‘Šè‚ÌF‚Æ‚µ‚ÄU‚é•‘‚¤
        return (myColor == StoneColor.Black) ? StoneColor.White : StoneColor.Black;
    }

    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult result)
    {
        board.SetCell(move.Pos.x, move.Pos.y, move.PlayerColor, move.Type);
        result.Effects.Add(new EffectEvent { Type = StoneType.Spy, Origin = move.Pos });
    }
}
