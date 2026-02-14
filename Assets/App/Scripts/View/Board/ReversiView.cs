using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class ReversiView : MonoBehaviour
{
    [SerializeField] private GameObject _stonePrefab;
    [SerializeField] private BoardMeshGenerator _boardGenerator; // 盤面生成器
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private float _cellSize = 1.0f;
    [SerializeField] private AutoCameraController _cameraController;
    [SerializeField] private BoardCursor _boardCursor;

    private Dictionary<int, GameObject> _activeStones = new Dictionary<int, GameObject>();
    private BoardState _currentBoard;

    private void Update()
    {
        if (_currentBoard == null || _boardCursor == null) return;

        // マウス位置からレイを飛ばす
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // UIの上にマウスがある場合はカーソルを消す（誤クリック防止）
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            _boardCursor.Hide();
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // グリッド座標に変換できるかトライ
            if (TryWorldToGrid(hit.point, out Position gridPos))
            {
                // 有効なマスの中心座標を計算
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

    // アニメーション付き更新
    public async UniTask AnimateMoveResultAsync(BoardState board, MoveResult result)
    {
        _currentBoard = board;

        // 拡張が発生していた場合、View管理下の石のキーも全て (+1, +1) にずらす
        if (result.WasBoardExpanded)
        {
            ShiftActiveStonesForExpansion();
        }

        // 1. 石の配置 (Spawn)
        // 拡張後の盤面では、元の座標(x,y)は(x+1, y+1)にシフトしている
        int px = result.PlacedPosition.x;
        int py = result.PlacedPosition.y;

        if (result.WasBoardExpanded)
        {
            px += 1;
            py += 1;
        }

        int placeKey = py * BoardState.MAX_SIZE + px;
        Vector3 placePos = CalculateWorldPos(px, py, board.Width, board.Height);

        // Phantomの場合でも、一瞬は表示する
        GameObject placedObj = null;

        if (!_activeStones.TryGetValue(placeKey, out placedObj))
        {
            placedObj = Instantiate(_stonePrefab, _boardRoot);
            _activeStones[placeKey] = placedObj;
        }

        // 位置と回転リセット
        placedObj.transform.localPosition = placePos;
        placedObj.transform.localRotation = Quaternion.identity;
        placedObj.SetActive(true); // 念のため

        var stoneView = placedObj.GetComponent<StoneView>();

        // Spyの場合は「相手の色」として出現させる
        StoneColor spawnColor = result.Move.PlayerColor;
        if (result.Move.Type == StoneType.Spy)
        {
            spawnColor = (result.Move.PlayerColor == StoneColor.Black) ? StoneColor.White : StoneColor.Black;
        }

        stoneView.SetAppearance(spawnColor, result.Move.Type);

        // プレハブの正規スケールを渡してアニメーションさせる
        // 置いた音を鳴らす
        GameAudioManager.Instance.PlayStoneSpawn(result.Move.Type);
        await stoneView.AnimateSpawnAsync(_stonePrefab.transform.localScale);

        // 2. 反転演出 (Flip)
        var flipTasks = new List<UniTask>();
        float delayPerStep = 0.05f;

        foreach (var pos in result.FlippedPositions)
        {
            // 反転対象の座標も、拡張時はずれているので補正が必要
            int fx = pos.x;
            int fy = pos.y;
            if (result.WasBoardExpanded)
            {
                fx += 1;
                fy += 1;
            }

            int key = fy * BoardState.MAX_SIZE + fx;
            if (_activeStones.TryGetValue(key, out GameObject obj))
            {
                var view = obj.GetComponent<StoneView>();
                // 色情報は補正後の座標で取得
                var cell = board.GetCell(fx, fy);

                flipTasks.Add(UniTask.Create(async () =>
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(delayPerStep));

                    // ひっくり返る音（NormalのSpawn音で代用可）
                    GameAudioManager.Instance.PlayStoneSpawn(StoneType.Normal);

                    // 固定石なら反転ではなく「拒絶（ガタガタ）」アニメーション
                    if (cell.Type == StoneType.Fixed || cell.IsFixed)
                    {
                        // 色は変わらないので、現在のアニメーションだけ再生
                        await view.AnimateLockedAsync();
                    }
                    else
                    {
                        // 通常の反転
                        await view.AnimateFlipAsync(cell.Color, cell.Type);
                    }
                }));
                delayPerStep += 0.05f;
            }
        }

        if (flipTasks.Count > 0)
        {
            await UniTask.WhenAll(flipTasks);
        }

        // 3. 特殊効果演出 (Effects)
        foreach (var effect in result.Effects)
        {
            // エフェクト発動音
            GameAudioManager.Instance.PlayStoneEffect(effect.Type);
            if (effect.Type == StoneType.Expander)
            {
                UpdateVisualsSync(board);
                await UniTask.Delay(500);
            }
            else if (effect.Type == StoneType.Bomb)
            {
                if (effect.AffectedPositions != null)
                {
                    var destroyTasks = new List<UniTask>();
                    foreach (var dPos in effect.AffectedPositions)
                    {
                        // 破壊対象の座標補正
                        int dx = dPos.x;
                        int dy = dPos.y;
                        if (result.WasBoardExpanded)
                        {
                            dx += 1;
                            dy += 1;
                        }

                        int dKey = dy * BoardState.MAX_SIZE + dx;
                        if (_activeStones.TryGetValue(dKey, out GameObject dObj))
                        {
                            var dView = dObj.GetComponent<StoneView>();
                            destroyTasks.Add(dView.AnimateDestructionAsync());
                        }
                    }
                    await UniTask.WhenAll(destroyTasks);
                }
            }
            else if (effect.Type == StoneType.Phantom)
            {
                // 自己消滅 (PhantomはPlacedPositionと同じ場所なので補正済みpx, pyを使う)
                int key = py * BoardState.MAX_SIZE + px;
                if (_activeStones.TryGetValue(key, out GameObject pObj))
                {
                    var pView = pObj.GetComponent<StoneView>();
                    await pView.AnimateDestructionAsync();
                }
            }
            else if (effect.Type == StoneType.Spy)
            {
                // Spyの変装解除（正体を現す）
                // 座標は配置位置と同じ（SpyはExpanderではないので座標補正は不要だが、px/pyは計算済みなのでそれを使う）
                int key = py * BoardState.MAX_SIZE + px;
                if (_activeStones.TryGetValue(key, out GameObject sObj))
                {
                    var sView = sObj.GetComponent<StoneView>();
                    // 本来の色（Move.Color）に戻るアニメーション
                    await sView.AnimateFlipAsync(result.Move.PlayerColor, StoneType.Spy);
                }
            }
        }

        // 4. 最終整合性チェック
        UpdateVisualsSync(board);
    }

    /// <summary>
    /// 盤面拡張時に、現在のアクティブな石のキーを全て (+1, +1) にずらす
    /// </summary>
    private void ShiftActiveStonesForExpansion()
    {
        var newActiveStones = new Dictionary<int, GameObject>();

        foreach (var kvp in _activeStones)
        {
            // 現在のキーからグリッド座標を復元
            int oldY = kvp.Key / BoardState.MAX_SIZE;
            int oldX = kvp.Key % BoardState.MAX_SIZE;

            // 新しい座標 (+1, +1)
            int newX = oldX + 1;
            int newY = oldY + 1;

            // 新しいキーを計算
            int newKey = newY * BoardState.MAX_SIZE + newX;

            // 新しい辞書に登録
            newActiveStones[newKey] = kvp.Value;
        }

        // 辞書を差し替え
        _activeStones = newActiveStones;
    }

    private void UpdateVisualsSync(BoardState board)
    {
        if (_boardGenerator != null) _boardGenerator.Generate(board.Width, _cellSize);
        if (_cameraController != null) _cameraController.UpdateTargetPosition(board.Width * _cellSize);

        float startX = -(board.Width * _cellSize) / 2.0f + (_cellSize / 2.0f);
        float startZ = -(board.Height * _cellSize) / 2.0f + (_cellSize / 2.0f);

        // 全マス走査して同期
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var cell = board.GetCell(x, y);
                int key = y * BoardState.MAX_SIZE + x;
                Vector3 pos = new Vector3(startX + x * _cellSize, 0, startZ + y * _cellSize);

                if (!cell.IsEmpty)
                {
                    if (!_activeStones.ContainsKey(key))
                    {
                        // 足りない石を補充
                        GameObject obj = Instantiate(_stonePrefab, _boardRoot);
                        obj.transform.localPosition = pos;
                        obj.transform.localRotation = Quaternion.identity; // 回転リセット
                        _activeStones[key] = obj;

                        // 色設定
                        obj.GetComponent<StoneView>().SetAppearance(cell.Color, cell.Type);
                    }
                    else
                    {
                        // 位置補正
                        _activeStones[key].transform.localPosition = pos;
                        // 色強制同期
                        _activeStones[key].GetComponent<StoneView>().SetAppearance(cell.Color, cell.Type);
                    }
                }
                else
                {
                    // 余分な石を削除
                    if (_activeStones.ContainsKey(key))
                    {
                        Destroy(_activeStones[key]);
                        _activeStones.Remove(key);
                    }
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
