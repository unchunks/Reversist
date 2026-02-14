using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class HumanPlayer : IPlayer
{
    private LayerMask _hitLayer = LayerMask.GetMask("Board");
    private ReversiView _view;
    private StoneSelectorUI _selectorUI;

    public HumanPlayer(ReversiView view, StoneSelectorUI selectorUI)
    {
        _view = view;
        _selectorUI = selectorUI;
    }

    public async UniTask<PlayerMove> DecideMoveAsync(BoardState board, StoneColor playerColor, StoneInventory inventory, CancellationToken token)
    {
        // 入力を待機
        while (true)
        {
            await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0), cancellationToken: token);

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // Raycastはあくまで「何かに当たったか」を判定するために使う
            // LayerMaskを設定して盤面のみに当たるようにするのが望ましい
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _hitLayer))
            {
                // 【修正点】座標計算ロジックをViewに委譲
                // これにより、盤面がどこにあろうと、回転していようと、正しいグリッド座標が返ってくる
                if (_view.TryWorldToGrid(hit.point, out Position pos))
                {
                    return new PlayerMove
                    {
                        Pos = pos,
                        PlayerColor = playerColor,
                        Type = _selectorUI.CurrentSelection
                    };
                }
            }
        }
    }
}
