using System.Collections.Generic;

public static class ReversiRules
{
    // 8方向の移動ベクトル（ラジアンと同じ順）
    private static readonly int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] dy = { 0, -1, -1, -1, 0, 1, 1, 1 };

    public static bool IsValidMove(BoardState board, PlayerMove move)
    {
        if (!board.GetCell(move.Pos.x, move.Pos.y).IsEmpty)
            return false;

        StoneStrategy strategy = StoneRegistry.Get(move.Type);
        StoneColor attackColor = strategy.GetAttackColor(move.PlayerColor);

        for (int i = 0; i < 8; i++)
        {
            if (CanFlipDirection(board, move.Pos, dx[i], dy[i], attackColor))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 手を適用し、発生したイベントログ(MoveResult)を返す。
    /// </summary>
    public static MoveResult ApplyMove(BoardState board, PlayerMove move)
    {
        var result = new MoveResult();
        result.PlacedPosition = move.Pos;
        result.Move = move;

        // 1. 石を置く
        StoneStrategy strategy = StoneRegistry.Get(move.Type);
        StoneColor finalColor = move.PlayerColor;
        StoneColor checkColor = strategy.GetAttackColor(move.PlayerColor);

        board.SetCell(move.Pos.x, move.Pos.y, checkColor, move.Type);

        // 2. 反転処理

        for (int i = 0; i < 8; i++)
        {
            GetFlipsInDirection(board, move.Pos, dx[i], dy[i], checkColor, result.FlippedPositions);
        }

        foreach (var pos in result.FlippedPositions)
        {
            var targetCell = board.GetCell(pos.x, pos.y);
            if (targetCell.IsFixed || targetCell.Type == StoneType.Fixed) continue;

            board.SetCell(pos.x, pos.y, checkColor, targetCell.Type);
        }

        // 3. 特殊効果の実行 (MoveResultを渡して記録させる)
        strategy.OnAfterPlacement(board, move, result.FlippedPositions, result);

        return result;
    }

    private static StoneColor GetOpposite(StoneColor c) => c == StoneColor.Black ? StoneColor.White : StoneColor.Black;

    private static bool CanFlipDirection(BoardState board, Position start, int dx, int dy, StoneColor attackColor)
    {
        int x = start.x + dx;
        int y = start.y + dy;
        bool hasOpponent = false;

        while (true)
        {
            var cell = board.GetCell(x, y);
            if (cell.Color == StoneColor.Wall || cell.Color == StoneColor.None)
                return false;

            if (cell.Color != attackColor)
            {
                hasOpponent = true;
            }
            else
            {
                return hasOpponent;
            }
            x += dx;
            y += dy;
        }
    }

    private static void GetFlipsInDirection(BoardState board, Position start, int dx, int dy, StoneColor myColor, List<Position> outFlips)
    {
        int x = start.x + dx;
        int y = start.y + dy;
        List<Position> temp = new List<Position>();

        while (true)
        {
            var cell = board.GetCell(x, y);
            if (cell.Color == StoneColor.Wall || cell.Color == StoneColor.None)
                return;

            if (cell.Color != myColor)
            {
                temp.Add(new Position(x, y));
            }
            else
            {
                outFlips.AddRange(temp);
                return;
            }
            x += dx;
            y += dy;
        }
    }

    /// <summary>
    /// 指定したプレイヤーに「打てる手」が存在するかチェックする。
    /// インベントリの在庫も考慮する。
    /// TODO: 全マス走査は重いので、将来的に最適化の余地あり。
    /// </summary>
    public static bool HasValidMove(BoardState board, StoneColor playerColor, StoneInventory inventory)
    {
        // 使用可能な石のリストを作成
        // Normalは常にチェック対象
        var usableTypes = new List<StoneType>();
        foreach (var kvp in inventory.Stock)
        {
            if (inventory.CanUse(kvp.Key))
            {
                usableTypes.Add(kvp.Key);
            }
        }

        // 全マス走査
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.GetCell(x, y).IsEmpty) continue;

                var pos = new Position(x, y);

                // 全種類の石で試行
                foreach (var type in usableTypes)
                {
                    var move = new PlayerMove
                    {
                        Pos = pos,
                        Type = type,
                        PlayerColor = playerColor
                    };

                    // IsValidMove自体が重い場合は、ここをインライン展開して最適化する余地があるが、
                    // 現状はコードの共通化を優先する。
                    if (IsValidMove(board, move))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
