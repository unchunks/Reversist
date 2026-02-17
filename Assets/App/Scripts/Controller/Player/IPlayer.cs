using Cysharp.Threading.Tasks;
using System.Threading;

public interface IPlayer
{
    UniTask<PlayerMove> DecideMoveAsync(BoardState board, StoneColor playerColor, StoneInventory inventory, CancellationToken token);
}
