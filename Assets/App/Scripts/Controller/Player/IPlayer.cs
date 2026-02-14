using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

public interface IPlayer
{
    UniTask<PlayerMove> DecideMoveAsync(BoardState board, StoneColor colplayerColoror, StoneInventory inventory, CancellationToken token);
}
