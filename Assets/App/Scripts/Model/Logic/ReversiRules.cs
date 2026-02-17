using System;
using System.Collections.Generic;

public static class ReversiRules
{
    private static readonly int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] dy = { 0, -1, -1, -1, 0, 1, 1, 1 };

    public static bool IsValidMove(BoardState board, PlayerMove move)
    {
        // 既に石がある場合は置けない
        if (!board.GetCell(move.Pos.x, move.Pos.y).IsEmpty)
            return false;

        StoneStrategy strategy = StoneRegistry.Get(move.Type);
        StoneColor attackColor = strategy.GetAttackColor(move.PlayerColor);

        for (int i = 0; i < 8; i++)
        {
            if (CheckOrCollectFlips(board, move.Pos, dx[i], dy[i], attackColor, null))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 盤面に手を適用し、裏返した石の位置や特殊効果の内容をMoveResultにまとめて返す
    /// </summary>
    public static MoveResult ApplyMove(BoardState board, PlayerMove move)
    {
        var result = new MoveResult();
        result.PlacedPosition = move.Pos;
        result.Move = move;

        StoneStrategy strategy = StoneRegistry.Get(move.Type);
        StoneColor checkColor = strategy.GetAttackColor(move.PlayerColor);

        // 8方向を探索して、反転する石をリストに追加する
        result.FlippedPositions.Clear();
        for (int i = 0; i < 8; i++)
        {
            CheckOrCollectFlips(board, move.Pos, dx[i], dy[i], checkColor, result.FlippedPositions);
        }

        board.SetCell(move.Pos.x, move.Pos.y, checkColor, move.Type);
        // 石を反転
        int flipCount = result.FlippedPositions.Count;
        for (int i = 0; i < flipCount; i++)
        {
            var pos = result.FlippedPositions[i];
            var targetCell = board.GetCell(pos.x, pos.y);
            if (targetCell.IsFixed || targetCell.Type == StoneType.Fixed) continue;

            board.SetCell(pos.x, pos.y, checkColor, targetCell.Type);
        }

        // 石の種類による特殊効果
        strategy.OnAfterPlacement(board, move, result.FlippedPositions, result);
        return result;
    }

    /// <summary>
    /// 特定の方向に対して裏返せる石があるか判定、またはリストへ追加を行う
    /// </summary>
    /// <param name="outFlips">null以外を渡すと、裏返せる石の座標がこのリストに追加される</param>
    /// <returns>裏返せる石があった場合はtrue</returns>
    private static bool CheckOrCollectFlips(BoardState board, Position start, int dx, int dy, StoneColor myColor, List<Position> outFlips)
    {
        int x = start.x + dx;
        int y = start.y + dy;

        // 相手の石が少なくとも1つあるか
        bool hasOpponent = false;

        // 仮のリスト作成は避け、距離（石の数）で管理する
        int count = 0;

        while (true)
        {
            var cell = board.GetCell(x, y);

            // 壁か空マスに到達したら、挟めていないので終了
            if (cell.Color == StoneColor.Wall || cell.Color == StoneColor.None)
                return false;

            // 自分の色に到達した場合
            if (cell.Color == myColor)
            {
                // 相手を挟まずに自分の石に当たった場合は終了
                if (!hasOpponent) return false;

                // リストが必要なければ早期リターン
                if (outFlips == null) return true;

                // スタート位置から現在位置の手前までをリストに追加
                int cx = start.x + dx;
                int cy = start.y + dy;
                for (int i = 0; i < count; i++)
                {
                    outFlips.Add(new Position(cx, cy));
                    cx += dx;
                    cy += dy;
                }
                return true;

            }

            // ここに来るのは、相手の石の場合のみ
            hasOpponent = true;
            count++;

            x += dx;
            y += dy;
        }
    }

    public static bool HasValidMove(BoardState board, StoneColor playerColor, StoneInventory inventory)
    {
        Span<StoneType> usableTypes = stackalloc StoneType[(int)StoneType.Size];
        int usableCount = inventory.GetAvailableStoneTypesNonAlloc(usableTypes);

        PlayerMove testMove = new PlayerMove { PlayerColor = playerColor };

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                // 空きマス以外はスキップ
                if (!board.GetCell(x, y).IsEmpty) continue;

                testMove.Pos.x = x;
                testMove.Pos.y = y;

                for (int i = 0; i < usableCount; i++)
                {
                    testMove.Type = usableTypes[i];
                    if (IsValidMove(board, testMove)) return true;
                }
            }
        }
        return false;
    }
}
