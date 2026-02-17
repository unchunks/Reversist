using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class LogicAI : IReversiAI
{
    private int _maxDepth;
    private int _beamWidth = 5;
    private StoneColor _myColor;

    // メモリプーリング（深さごとの状態キャッシュ）
    // 探索ツリーの各深さで使い回すための盤面とインベントリ
    private BoardState[] _boardPool;
    private StoneInventory[] _inventoryPool;

    // 候補手の一時保存用バッファ
    private struct MoveCandidate : IComparable<MoveCandidate>
    {
        public PlayerMove Move;
        public int Score;
        public int CompareTo(MoveCandidate other) => other.Score.CompareTo(this.Score); // 降順
    }

    // REVIEW: _candidatePoolと_validMovesPoolはまとめず分けた方が、処理的に速い...らしい
    /// <summary>
    /// 候補主を打ったときの盤面評価スコアを格納するプール（深さごとに使い回す）
    /// </summary>
    private MoveCandidate[][] _candidatePool;
    /// <summary>
    /// 深さごとの次に打てる候補手を格納するプール（深さごとに使い回す）
    /// </summary>
    private List<PlayerMove>[] _validMovesPool;
    private const int MAX_CANDIDATES = BoardState.MAX_SIZE * BoardState.MAX_SIZE * (int)StoneType.Size; // 1盤面での候補手の最大数（実際はもっと少ないはず）

    public LogicAI(int depth)
    {
        _maxDepth = depth;

        // 深さ分（ルート+探索深さ+余裕分）のプールを初期化
        int poolSize = depth + 2;
        _boardPool = new BoardState[poolSize];
        _inventoryPool = new StoneInventory[poolSize];
        _candidatePool = new MoveCandidate[poolSize][];
        _validMovesPool = new List<PlayerMove>[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            _boardPool[i] = new BoardState();
            _inventoryPool[i] = new StoneInventory();
            _candidatePool[i] = new MoveCandidate[MAX_CANDIDATES];
            _validMovesPool[i] = new List<PlayerMove>(MAX_CANDIDATES);
        }
    }

    public PlayerMove CalculateNextMove(BoardState currentBoard, StoneColor myColor, StoneInventory inventory, CancellationToken token)
    {
        _myColor = myColor;
        int currentDepthIndex = _maxDepth; // ルートを最大インデックスとする

        // ルートの有効手を取得
        List<PlayerMove> moves = GetValidMovesFast(currentBoard, myColor, inventory, _validMovesPool[currentDepthIndex]);

        if (moves.Count == 0)
        {
            Debug.LogWarning("LogicAI: 打てる手がないのにAIに手番が回っています");
            return PlayerMove.Invalid;
        }
        if (moves.Count == 1) return moves[0];

        // 浅い評価とソート
        var candidates = _candidatePool[currentDepthIndex];
        int candidateCount = 0;

        foreach (var move in moves)
        {
            // 次の深さのプールを使う
            int nextIndex = currentDepthIndex - 1;
            currentBoard.CopyTo(_boardPool[nextIndex]);
            inventory.CopyTo(_inventoryPool[nextIndex]);

            // 適用
            ReversiRules.ApplyMove(_boardPool[nextIndex], move);
            _inventoryPool[nextIndex].Use(move.Type);

            int score = BoardEvaluator.EvaluateWithMove(_boardPool[nextIndex], myColor, move);

            candidates[candidateCount++] = new MoveCandidate { Move = move, Score = score };
        }

        // 降順ソート
        Array.Sort(candidates, 0, candidateCount);

        if (_maxDepth <= 1) return candidates[0].Move;

        // 深層探索 (ビーム幅分だけ)
        PlayerMove bestMove = candidates[0].Move;
        int bestDeepScore = int.MinValue;
        int beamLimit = Mathf.Min(_beamWidth, candidateCount);

        for (int i = 0; i < beamLimit; i++)
        {
            var move = candidates[i].Move;
            int nextIndex = currentDepthIndex - 1;

            // 再度状態をセットアップ
            currentBoard.CopyTo(_boardPool[nextIndex]);
            inventory.CopyTo(_inventoryPool[nextIndex]);
            ReversiRules.ApplyMove(_boardPool[nextIndex], move);
            _inventoryPool[nextIndex].Use(move.Type);

            // Negamax
            // 相手基準での最善手のスコアを取得するため、-をつける
            int score = -SearchRecursive(_boardPool[nextIndex], myColor.GetOpposite(), _maxDepth - 1, _inventoryPool[nextIndex], nextIndex);

            if (score > bestDeepScore)
            {
                bestDeepScore = score;
                bestMove = move;
            }
        }

        return bestMove;
    }

    /// <summary>
    /// 渡された盤面の中から最善手を探してスコアを返す
    /// </summary>
    /// <param name="depth">今回を含めた、残りの探索すべき深さ</param>
    /// <param name="poolIndex">この値の1つ深いインデックスに入る手を探す</param>
    private int SearchRecursive(BoardState board, StoneColor currentColor, int depth, StoneInventory currentInventory, int poolIndex)
    {
        if (depth == 0)
        {
            return BoardEvaluator.Evaluate(board, currentColor);
        }

        // 相手番のインベントリは自分と同じと仮定して計算
        StoneInventory activeInv = (currentColor == _myColor) ? currentInventory : _inventoryPool[_maxDepth]; // ルートは常に初期状態

        var moves = GetValidMovesFast(board, currentColor, activeInv, _validMovesPool[poolIndex]);

        if (moves.Count == 0)
        {
            // パス
            return -SearchRecursive(board, currentColor.GetOpposite(), depth - 1, currentInventory, poolIndex);
        }

        var candidates = _candidatePool[poolIndex];
        int candidateCount = 0;

        // 候補手を探して評価
        foreach (var move in moves)
        {
            int nextIndex = poolIndex - 1;
            board.CopyTo(_boardPool[nextIndex]);
            currentInventory.CopyTo(_inventoryPool[nextIndex]);

            ReversiRules.ApplyMove(_boardPool[nextIndex], move);
            if (currentColor == _myColor) _inventoryPool[nextIndex].Use(move.Type);

            int score = BoardEvaluator.EvaluateWithMove(_boardPool[nextIndex], currentColor, move);
            candidates[candidateCount++] = new MoveCandidate { Move = move, Score = score };
        }

        // 一つ下へ再帰探索
        Array.Sort(candidates, 0, candidateCount);
        int beamLimit = Mathf.Min(_beamWidth, candidateCount);
        int bestVal = int.MinValue;

        for (int i = 0; i < beamLimit; i++)
        {
            var move = candidates[i].Move;
            int nextIndex = poolIndex - 1;

            board.CopyTo(_boardPool[nextIndex]);
            currentInventory.CopyTo(_inventoryPool[nextIndex]);
            ReversiRules.ApplyMove(_boardPool[nextIndex], move);
            if (currentColor == _myColor) _inventoryPool[nextIndex].Use(move.Type);

            int val = -SearchRecursive(_boardPool[nextIndex], currentColor.GetOpposite(), depth - 1, _inventoryPool[nextIndex], nextIndex);

            if (val > bestVal) bestVal = val;
        }

        return bestVal;
    }

    #region Helper Methods

    private List<PlayerMove> GetValidMovesFast(BoardState board, StoneColor color, StoneInventory inventory, List<PlayerMove> buffer)
    {
        buffer.Clear();

        // Useable Typesのキャッシュ（スタックベースの配列で十分）
        Span<StoneType> usableTypes = stackalloc StoneType[6];
        int usableCount = 0;
        for (int i = 0; i < (int)StoneType.Size; i++)
        {
            if (inventory.CanUse((StoneType)i)) usableTypes[usableCount++] = (StoneType)i;
        }

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.GetCell(x, y).IsEmpty) continue;

                var pos = new Position(x, y);
                for (int i = 0; i < usableCount; i++)
                {
                    var move = new PlayerMove { Pos = pos, Type = usableTypes[i], PlayerColor = color };
                    if (ReversiRules.IsValidMove(board, move)) buffer.Add(move);
                }
            }
        }
        return buffer;
    }

    #endregion
}
