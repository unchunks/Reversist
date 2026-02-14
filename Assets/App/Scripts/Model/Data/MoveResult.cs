using System.Collections.Generic;

// ---------------------------------------------------------
// DATA: Turn Event Log
// 1手によって引き起こされた全ての事象を記録するコンテナ
// ---------------------------------------------------------

public class MoveResult
{
    public PlayerMove Move;

    public Position PlacedPosition;

    // 反転した石のリスト（順序付き）
    public List<Position> FlippedPositions = new List<Position>();

    // 発生した特殊効果のリスト
    public List<EffectEvent> Effects = new List<EffectEvent>();

    // 盤面が拡張されたか？
    public bool WasBoardExpanded = false;
}

public struct EffectEvent
{
    public StoneType Type;       // 発動した効果の種類
    public Position Origin;      // 発動源
    public List<Position> AffectedPositions; // 影響を受けた座標（破壊された石など）
}
