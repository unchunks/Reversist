using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class LogicAI : IReversiAI
{
    private int _maxDepth;
    private int _beamWidth = 5;
    private StoneColor _myColor; // AI自身の色を記憶

    public LogicAI(int depth)
    {
        _maxDepth = depth;
    }

    public PlayerMove CalculateNextMove(BoardState currentBoard, StoneColor myColor, StoneInventory inventory)
    {
        _myColor = myColor; // 色を保存

        StringBuilder log = new StringBuilder();
        log.AppendLine($"<color=cyan>[AI START]</color> Me:{myColor} Depth:{_maxDepth} (Tracing All Moves)");

        // 1. ルートノード（自分の手番）
        var moves = GetAllValidMoves(currentBoard, myColor, inventory);

        if (moves.Count == 0)
        {
            Debug.Log(log.ToString() + "\nResult: PASS");
            return PlayerMove.Invalid;
        }

        if (moves.Count == 1)
        {
            Debug.Log(log.ToString() + "\nResult: ONLY ONE MOVE");
            return moves[0];
        }

        // 2. 第1段階: 全候補の浅い評価
        var firstLevelCandidates = new List<(PlayerMove Move, int Score, BoardState State, StoneInventory Inv)>();

        log.AppendLine("[Root Analysis]");
        foreach (var move in moves)
        {
            var nextBoard = new BoardState(currentBoard);
            ReversiRules.ApplyMove(nextBoard, move);

            // 自分の手番なので、自分のインベントリを消費する
            var nextInventory = CloneInventory(inventory);
            nextInventory.Use(move.Type);

            int score = BoardEvaluator.Evaluate(nextBoard, myColor);

            firstLevelCandidates.Add((move, score, nextBoard, nextInventory));

            // ログ: 候補手とその浅い評価値
            log.AppendLine($" - Cand: ({move.Pos.x},{move.Pos.y})[{move.Type}] ShallowEval:{score}");
        }

        // 3. ビーム選択
        var topCandidates = firstLevelCandidates
            .OrderByDescending(c => c.Score)
            .Take(_beamWidth)
            .ToList();

        // 深さ1なら即決
        if (_maxDepth <= 1)
        {
            Debug.Log(log.ToString() + $"\nDepth 1 Decision: {topCandidates[0].Move.Pos.x},{topCandidates[0].Move.Pos.y}");
            return topCandidates[0].Move;
        }

        // 4. 深層探索
        PlayerMove bestMove = topCandidates[0].Move;
        int bestDeepScore = int.MinValue;

        log.AppendLine("\n[Deep Search Tree]");

        foreach (var candidate in topCandidates)
        {
            string currentPath = $"({candidate.Move.Pos.x},{candidate.Move.Pos.y})";

            // 再帰探索開始（ログビルダーとパスを渡す）
            // Negamax: score = -Search(...)
            int score = -SearchRecursive(candidate.State, GetOpposite(myColor), _maxDepth - 1, candidate.Inv, log, currentPath);

            log.AppendLine($"<color=white>=> Root Branch {currentPath} Final Score: {score}</color>");

            if (score > bestDeepScore)
            {
                bestDeepScore = score;
                bestMove = candidate.Move;
            }
        }

        log.AppendLine($"\n<color=yellow>FINAL DECISION: ({bestMove.Pos.x},{bestMove.Pos.y}) Score:{bestDeepScore}</color>");
        Debug.Log(log.ToString());
        return bestMove;
    }

    // 再帰探索 (Logging版)
    private int SearchRecursive(BoardState board, StoneColor currentColor, int depth, StoneInventory aiInventory, StringBuilder log, string path)
    {
        // インデント生成（視認性向上）
        string indent = new string(' ', (_maxDepth - depth) * 2);

        if (depth == 0)
        {
            // 末端評価
            int eval = BoardEvaluator.Evaluate(board, currentColor);
            // 葉ノードのログ出力
            log.AppendLine($"{indent}Leaf {path}: Eval {eval}");
            return eval;
        }

        // --- インベントリの切り替えロジック ---
        StoneInventory currentTurnInventory;
        if (currentColor == _myColor)
        {
            currentTurnInventory = aiInventory;
        }
        else
        {
            // 相手番はデフォルトインベントリ（Normal無限）と仮定
            currentTurnInventory = new StoneInventory();
        }

        var moves = GetAllValidMoves(board, currentColor, currentTurnInventory);

        // パスの場合
        if (moves.Count == 0)
        {
            log.AppendLine($"{indent}Pass at {path}");
            return -SearchRecursive(board, GetOpposite(currentColor), depth - 1, aiInventory, log, path + "->PASS");
        }

        // 候補生成
        var candidates = new List<(int Score, BoardState State, StoneInventory NextAIInv, PlayerMove Move)>();

        foreach (var move in moves)
        {
            var nextBoard = new BoardState(board);
            ReversiRules.ApplyMove(nextBoard, move);

            // 消費ロジック
            StoneInventory nextAiInventoryForChild;
            if (currentColor == _myColor)
            {
                var nextInv = CloneInventory(aiInventory);
                nextInv.Use(move.Type);
                nextAiInventoryForChild = nextInv;
            }
            else
            {
                nextAiInventoryForChild = aiInventory;
            }

            int score = BoardEvaluator.Evaluate(nextBoard, currentColor);
            candidates.Add((score, nextBoard, nextAiInventoryForChild, move));
        }

        // ビームカット (深層でも有望な手だけを追う)
        var topCandidates = candidates.OrderByDescending(c => c.Score).Take(_beamWidth);

        int bestVal = int.MinValue;
        foreach (var candidate in topCandidates)
        {
            string nextPath = $"{path}->({candidate.Move.Pos.x},{candidate.Move.Pos.y})";

            // 再帰呼び出し
            int val = -SearchRecursive(candidate.State, GetOpposite(currentColor), depth - 1, candidate.NextAIInv, log, nextPath);

            if (val > bestVal) bestVal = val;
        }

        return bestVal;
    }

    private List<PlayerMove> GetAllValidMoves(BoardState board, StoneColor color, StoneInventory inventory)
    {
        var moves = new List<PlayerMove>();

        var usableTypes = new List<StoneType>();
        foreach (var kvp in inventory.Stock)
        {
            if (inventory.CanUse(kvp.Key)) usableTypes.Add(kvp.Key);
        }

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.GetCell(x, y).IsEmpty) continue;
                var pos = new Position(x, y);

                foreach (var type in usableTypes)
                {
                    var move = new PlayerMove { Pos = pos, Type = type, PlayerColor = color };
                    if (ReversiRules.IsValidMove(board, move)) moves.Add(move);
                }
            }
        }
        return moves;
    }

    private StoneInventory CloneInventory(StoneInventory source)
    {
        var clone = new StoneInventory();
        clone.Stock = new Dictionary<StoneType, int>(source.Stock);
        clone.LastSelected = source.LastSelected;
        return clone;
    }

    private StoneColor GetOpposite(StoneColor c) => c == StoneColor.Black ? StoneColor.White : StoneColor.Black;
}
