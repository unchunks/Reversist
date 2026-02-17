using Cysharp.Threading.Tasks;
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
        // 思考演出
        await UniTask.Delay(500, cancellationToken: token);


        // 盤面のコピー
        BoardState threadSafeBoard = new BoardState(board);

        PlayerMove bestMove = PlayerMove.Invalid;

        // AI思考実行
#if UNITY_WEBGL
        // --- WebGL環境 ---
        // WebGLではスレッドプールが使えないため、メインスレッドで実行する
        // UIのフリーズを防ぐため、1フレームだけ待って描画を更新させてから計算に入る
        await UniTask.Yield(token);
        bestMove = _ai.CalculateNextMove(threadSafeBoard, playerColor, inventory, CancellationToken.None);
#else
        // --- スタンドアロン/モバイル環境 ---
        try
        {
            return await UniTask.RunOnThreadPool(() => 
            {                
                if (_ai is ICancellableAI cancellableAI)
                {
                    return cancellableAI.CalculateNextMove(threadSafeBoard, playerColor, inventory, token);
                }
                else
                {
                    return _ai.CalculateNextMove(threadSafeBoard, playerColor, inventory);
                }
            }, cancellationToken: token);
        }
        catch (System.OperationCanceledException)
        {
            throw;
        }
#endif

        return bestMove;
    }
}
