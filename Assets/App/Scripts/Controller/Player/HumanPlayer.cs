using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

public class HumanPlayer : IPlayer
{
    private ReversiView _view;
    private StoneSelectorUI _selectorUI;

    public HumanPlayer(ReversiView view, StoneSelectorUI selectorUI)
    {
        _view = view;
        _selectorUI = selectorUI;
    }

    public async UniTask<PlayerMove> DecideMoveAsync(BoardState board, StoneColor playerColor, StoneInventory inventory, CancellationToken token)
    {
        while (true)
        {
            // 左クリックが押されるまで待機
            await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0), cancellationToken: token);

            // UIを操作している場合はスキップ
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // GetMouseButtonDownなので、クリックをを離すまで待たなくてよい
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                continue;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // 盤面の高さ(Y=0)の数学的平面との交差判定を行う
            Plane boardPlane = new Plane(Vector3.up, Vector3.zero);

            if (boardPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);

                // 座標計算ロジックはViewで行う
                if (_view.TryWorldToGrid(hitPoint, out Position pos))
                {
                    // ReversiController側で IsValidMove を判定しているのでここではそのまま返す
                    return new PlayerMove
                    {
                        Pos = pos,
                        PlayerColor = playerColor,
                        Type = _selectorUI.CurrentSelection
                    };
                }
            }

            // 無効な場所をクリックした場合は次の入力を待つ
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
}
