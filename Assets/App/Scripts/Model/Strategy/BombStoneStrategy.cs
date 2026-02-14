using System.Collections.Generic;

public class BombStoneStrategy : StoneStrategy
{
    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult result)
    {
        var affected = new List<Position>();
        // ŽüˆÍ1ƒ}ƒX‚ð”j‰ó
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Position target = new Position(move.Pos.x + x, move.Pos.y + y);
                // •Ç‚Å‚È‚¯‚ê‚Î”j‰ó
                if (board.GetCell(target.x, target.y).Color != StoneColor.Wall)
                {
                    board.SetCell(target.x, target.y, StoneColor.None, StoneType.Normal);
                    affected.Add(target);
                }
            }
        }
        // ”j‰óƒƒO‹L˜^
        result.Effects.Add(new EffectEvent { Type = StoneType.Bomb, Origin = move.Pos, AffectedPositions = affected });
    }
}
