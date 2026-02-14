using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

public class CPUPlayer : IPlayer
{
    private IReversiAI _ai;

    public CPUPlayer(IReversiAI ai)
    {
        _ai = ai;
    }

    public async UniTask<PlayerMove> DecideMoveAsync(BoardState board, StoneColor playerColor, StoneInventory inventory, CancellationToken token)
    {
        // 思考演出ウェイト (0.5秒)
        await UniTask.Delay(500, cancellationToken: token);


        // 盤面のコピー
        BoardState threadSafeBoard = new BoardState(board);

        // AI思考実行
        PlayerMove bestMove = PlayerMove.Invalid;

        // WebGLではスレッドプールが使えないため、メインスレッドで実行する
        // ただし、計算が重すぎるとブラウザがフリーズするため、WebGL時は探索深さを下げるなどの調整が推奨される

#if UNITY_WEBGL
        // --- WebGL環境 ---
        // スレッド切り替えを行わず、メインスレッドで実行する。
        // UIのフリーズを防ぐため、1フレームだけ待って描画を更新させてから計算に入る
        await UniTask.Yield(token);

        bestMove = _ai.CalculateNextMove(threadSafeBoard, playerColor, inventory);
#else
        // --- スタンドアロン/モバイル環境 ---
        // バックグラウンドスレッドへ移動
        await UniTask.SwitchToThreadPool();

        // 重い計算
        bestMove = _ai.CalculateNextMove(threadSafeBoard, color, inventory);

        // メインスレッドへ戻る
        await UniTask.SwitchToMainThread(token);
#endif

        return bestMove;
    }
}
