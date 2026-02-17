using System.Collections.Generic;

public class BombStoneStrategy : StoneStrategy
{
    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult outResult)
    {
        List<Position> affected = (outResult != null) ? new List<Position>(8) : null;

        // ŽüˆÍ1ƒ}ƒX‚ð”j‰ó
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Position target = new Position(move.Pos.x + x, move.Pos.y + y);
                // •Ç‚Å‚È‚¯‚ê‚Î”j‰ó
                if (board.GetCell(target.x, target.y).Color == StoneColor.Wall) continue;
                board.SetCell(target.x, target.y, StoneColor.None, StoneType.Normal);

                if (x == 0 && y == 0) continue;
                affected?.Add(target);
            }
        }

        // ”j‰óƒƒO‹L˜^
        if (outResult != null)
        {
            outResult.Effect.Type = StoneType.Bomb;
            outResult.Effect.Origin = move.Pos;
            outResult.Effect.AffectedPositions = affected;
        }
    }
}
