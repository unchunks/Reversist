using System.Collections.Generic;

public class ExpanderStoneStrategy : StoneStrategy
{
    public override void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult result)
    {
        board.ExpandBoard();
        result.WasBoardExpanded = true; // イベント記録

        // エフェクト記録
        result.Effects.Add(new EffectEvent { Type = StoneType.Expander, Origin = move.Pos });
    }
}
