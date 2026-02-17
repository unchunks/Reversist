using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;

public class ReversiView : MonoBehaviour
{
    [SerializeField] private GameObject _stonePrefab;
    [SerializeField] private BoardMeshGenerator _boardGenerator; // 盤生成器
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private float _cellSize = 0.1f;    // 1マスの物理サイズ
    [SerializeField] private AutoCameraController _cameraController;
    [SerializeField] private BoardCursor _boardCursor;

    private GameObject[] _stonePool = new GameObject[BoardState.MAX_SIZE * BoardState.MAX_SIZE];
    private BoardState _currentBoard;

    private void Awake()
    {
        for (int i = 0; i < _stonePool.Length; i++)
        {
            var obj = Instantiate(_stonePrefab, _boardRoot);
            obj.SetActive(false);
            _stonePool[i] = obj;
        }
    }

    private void Update()
    {
        if (_currentBoard == null || _boardCursor == null) return;

        // UIの上にマウスがある場合はカーソルを消す（誤クリック防止）
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            _boardCursor.Hide();
            return;
        }

        // カーソルの表示位置を計算
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane boardPlane = new Plane(Vector3.up, _boardRoot.position);

        if (boardPlane.Raycast(ray, out float enter))
        {
            if (TryWorldToGrid(ray.GetPoint(enter), out Position gridPos))
            {
                Vector3 worldPos = CalculateWorldPos(gridPos.x, gridPos.y, _currentBoard.Width, _currentBoard.Height);
                _boardCursor.ShowAt(worldPos);
            }
            else
            {
                _boardCursor.Hide();
            }
        }
        else
        {
            _boardCursor.Hide();
        }
    }

    public void Initialize(BoardState board)
    {
        _currentBoard = board;
        UpdateVisualsSync(board);
    }

    // 即時更新（アニメーションなし）
    public void UpdateBoard(BoardState board)
    {
        _currentBoard = board;
        UpdateVisualsSync(board);
    }

    private GameObject GetStoneViewObject(int x, int y, BoardState board)
    {
        int realX = board.OriginX + x;
        int realY = board.OriginY + y;
        return _stonePool[realY * BoardState.MAX_SIZE + realX];
    }

    // アニメーション付き更新
    public async UniTask AnimateMoveResultAsync(BoardState board, MoveResult result, CancellationToken token)
    {
        _currentBoard = board;

        await PlaceAsync(board, result, token);

        await FlipAsync(board, result, token);

        await SpecialEffectAsync(board, result, token);

        // 整合性チェック
        UpdateVisualsSync(board);
    }

    private async UniTask PlaceAsync(BoardState board, MoveResult result, CancellationToken token)
    {
        // 拡張フラグが立っている場合の座標補正値
        int shift = result.WasBoardExpanded ? 1 : 0;

        int px = result.PlacedPosition.x + shift;
        int py = result.PlacedPosition.y + shift;

        GameObject placedObj = GetStoneViewObject(px, py, board);
        Vector3 placePos = CalculateWorldPos(px, py, board.Width, board.Height);

        placedObj.transform.localPosition = placePos;
        placedObj.transform.localRotation = Quaternion.identity;
        placedObj.SetActive(true);

        // 見た目の設定（色とタイプ）
        var stoneView = placedObj.GetComponent<StoneView>();
        StoneColor spawnColor = StoneRegistry.Get(result.Move.Type).GetAttackColor(result.Move.PlayerColor);
        stoneView.SetAppearance(spawnColor, result.Move.Type);

        // 配置アニメーション
        GameAudioManager.Instance.PlayStoneSpawn(result.Move.Type);
        await stoneView.AnimateSpawnAsync(_stonePrefab.transform.localScale).AttachExternalCancellation(token);
    }

    private async UniTask FlipAsync(BoardState board, MoveResult result, CancellationToken token)
    {
        int shift = result.WasBoardExpanded ? 1 : 0;
        var flipTasks = new List<UniTask>();
        float delayPerStep = 0.05f;

        foreach (var pos in result.FlippedPositions)
        {
            int px = pos.x + shift;
            int py = pos.y + shift;
            GameObject obj = GetStoneViewObject(px, py, board);
            var view = obj.GetComponent<StoneView>();
            var cell = board.GetCell(px, py); // 更新後の色とタイプを取得

            // 反転アニメーションを順番に実行するよう遅延させつつ登録
            flipTasks.Add(UniTask.Create(async () =>
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delayPerStep), cancellationToken: token);
                GameAudioManager.Instance.PlayStoneSpawn(StoneType.Normal);

                if (cell.IsFixed)
                {
                    await view.AnimateLockedAsync().AttachExternalCancellation(token);
                }
                else
                {
                    await view.AnimateFlipAsync(cell.Color, cell.Type).AttachExternalCancellation(token);
                }
            }));
            delayPerStep += 0.05f;
        }

        if (flipTasks.Count > 0)
        {
            await UniTask.WhenAll(flipTasks);
        }
    }

    private async UniTask SpecialEffectAsync(BoardState board, MoveResult result, CancellationToken token)
    {
        int shift = result.WasBoardExpanded ? 1 : 0;
        GameAudioManager.Instance.PlayStoneEffect(result.Effect.Type);

        int originX = result.Effect.Origin.x + shift;
        int originY = result.Effect.Origin.y + shift;

        switch (result.Effect.Type)
        {
            case StoneType.Expander:
                UpdateVisualsSync(board);
                await UniTask.Delay(500, cancellationToken: token);
                break;

            case StoneType.Bomb:
                if (result.Effect.AffectedPositions == null) break;
                var destroyTasks = new List<UniTask>();
                foreach (var dPos in result.Effect.AffectedPositions)
                {
                    GameObject dObj = GetStoneViewObject(dPos.x, dPos.y, board);
                    var dView = dObj.GetComponent<StoneView>();
                    destroyTasks.Add(dView.AnimateDestructionAsync().AttachExternalCancellation(token));
                }
                await UniTask.WhenAll(destroyTasks);
                break;

            case StoneType.Phantom:
                GameObject pObj = GetStoneViewObject(originX, originY, board);
                var pView = pObj.GetComponent<StoneView>();
                await pView.AnimateDestructionAsync().AttachExternalCancellation(token);
                break;

            case StoneType.Spy:
                GameObject sObj = GetStoneViewObject(originX, originY, board);
                var sView = sObj.GetComponent<StoneView>();
                await sView.AnimateFlipAsync(result.Move.PlayerColor, StoneType.Spy).AttachExternalCancellation(token);
                break;

        }
    }

    private void UpdateVisualsSync(BoardState board)
    {
        if (_boardGenerator != null) _boardGenerator.Generate(board.Width, _cellSize);
        if (_cameraController != null) _cameraController.UpdateTargetPosition(board.Width * _cellSize);

        float startX = -(board.Width * _cellSize) / 2.0f + (_cellSize / 2.0f);
        float startZ = -(board.Height * _cellSize) / 2.0f + (_cellSize / 2.0f);

        // プール全体を一旦非表示にする（整合性のため）
        // 毎回全クリアは少しコストだが144要素なので許容範囲
        for (int i = 0; i < _stonePool.Length; i++) _stonePool[i].SetActive(false);

        // 有効な石だけを表示・位置合わせ
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var cell = board.GetCell(x, y);
                if (!cell.IsEmpty)
                {
                    GameObject obj = GetStoneViewObject(x, y, board);
                    Vector3 pos = new Vector3(startX + x * _cellSize, 0, startZ + y * _cellSize);

                    obj.transform.localPosition = pos;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.SetActive(true);

                    obj.GetComponent<StoneView>().SetAppearance(cell.Color, cell.Type);
                }
            }
        }
    }

    private Vector3 CalculateWorldPos(int x, int y, int width, int height)
    {
        float startX = -(width * _cellSize) / 2.0f + (_cellSize / 2.0f);
        float startZ = -(height * _cellSize) / 2.0f + (_cellSize / 2.0f);
        return new Vector3(startX + x * _cellSize, 0, startZ + y * _cellSize);
    }

    /// <summary>
    /// クリック座標をグリッド座標に変換
    /// </summary>
    public bool TryWorldToGrid(Vector3 worldPoint, out Position gridPos)
    {
        gridPos = new Position(-1, -1);
        if (_currentBoard == null) return false;

        Vector3 localPos = _boardRoot.InverseTransformPoint(worldPoint);

        float boardPhysicalWidth = _currentBoard.Width * _cellSize;
        float boardPhysicalHeight = _currentBoard.Height * _cellSize;
        float startX = -boardPhysicalWidth / 2.0f;
        float startZ = -boardPhysicalHeight / 2.0f;

        int x = Mathf.FloorToInt((localPos.x - startX) / _cellSize);
        int y = Mathf.FloorToInt((localPos.z - startZ) / _cellSize);

        if (x >= 0 && x < _currentBoard.Width && y >= 0 && y < _currentBoard.Height)
        {
            gridPos = new Position(x, y);
            return true;
        }
        return false;
    }
}
