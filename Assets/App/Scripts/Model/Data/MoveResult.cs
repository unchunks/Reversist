using System.Collections.Generic;

public class MoveResult
{
    public PlayerMove Move;
    public Position PlacedPosition;

    public List<Position> FlippedPositions = new List<Position>(BoardState.MAX_SIZE * BoardState.MAX_SIZE);
    public EffectEvent Effect = default; // 現状すべての効果はターン内で完結するため、配列ではなく変数
    public bool WasBoardExpanded = false;

    /// <summary>
    /// 次のターンで使用する前に情報をリセットする
    /// </summary>
    public void Clear()
    {
        Move = PlayerMove.Invalid;
        PlacedPosition = new Position(-1, -1);
        FlippedPositions.Clear();
        Effect = default;
        WasBoardExpanded = false;
    }
}

public struct EffectEvent
{
    public StoneType Type;
    public Position Origin;
    public List<Position> AffectedPositions;
}
