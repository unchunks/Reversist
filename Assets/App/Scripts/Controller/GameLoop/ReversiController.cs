using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReversiController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ReversiView _view;
    [SerializeField] private GameUIView _ui;
    [SerializeField] private StoneSelectorUI _selectorUI;

    [SerializeField] private CanvasGroup _fadePanel;
    [SerializeField] private string _titleSceneName = "TitleScene";

    private BoardState _board;
    private StoneColor _currentPlayerColor;

    // ゲームループ全体のキャンセルを管理
    private CancellationTokenSource _gameLoopCts;

    private IPlayer _playerBlack;
    private IPlayer _playerWhite;

    private StoneInventory _blackInventory;
    private StoneInventory _whiteInventory;

    // 二重遷移防止フラグ
    private bool _isTransitioning = false;

    void Start()
    {
        _gameLoopCts = new CancellationTokenSource();
        InitializeGame();

        // フェードインとゲームループを並行して開始
        FadePanelAsync(1f, 0f, _gameLoopCts.Token).Forget();
        GameLoopAsync(_gameLoopCts.Token).Forget();
    }

    private void OnDestroy()
    {
        // ReturnToTitleAsyncで呼び出されるが、念のためここでもキャンセルしておく
        CancelGameLoop();
    }

    private void CancelGameLoop()
    {
        // 
        if (_gameLoopCts != null && !_gameLoopCts.IsCancellationRequested)
        {
            _gameLoopCts.Cancel();
            _gameLoopCts.Dispose();
            _gameLoopCts = null;
        }
    }

    private void InitializeGame()
    {
        // BGM切り替え
        GameAudioManager.Instance.PlayBGM(GameAudioManager.BgmType.MainGame);
        GameAudioManager.Instance.PlayGameStart();

        _board = new BoardState();
        _view.Initialize(_board);

        _blackInventory = new StoneInventory();
        _whiteInventory = new StoneInventory();

        // プレイヤー設定の読み込み
        if (GameSettings.Mode == GameSettings.GameMode.PvP)
        {
            _playerBlack = new HumanPlayer(_view, _selectorUI);
            _playerWhite = new HumanPlayer(_view, _selectorUI);

            // UI初期化 (黒番開始なので黒インベントリ)
            _selectorUI.Initialize(_blackInventory);
        }
        else // PvE
        {
            GameSettings.PlayerSide playerSide = GameSettings.Side;

            // Randomならここで決定
            if (playerSide == GameSettings.PlayerSide.Random)
            {
                playerSide = (UnityEngine.Random.Range(0, 2) == 0) ? GameSettings.PlayerSide.Black : GameSettings.PlayerSide.White;
            }

            int depth = GameSettings.AiDifficulty;
            IPlayer human = new HumanPlayer(_view, _selectorUI);
            IPlayer cpu = new CPUPlayer(new LogicAI(depth));

            if (playerSide == GameSettings.PlayerSide.Black)
            {
                // プレイヤーが黒（先攻）
                _playerBlack = human;
                _playerWhite = cpu;
                _selectorUI.Initialize(_blackInventory);
            }
            else
            {
                // プレイヤーが白（後攻）
                _playerBlack = cpu;
                _playerWhite = human;
                _selectorUI.Initialize(_whiteInventory);
            }
        }

        // リザルト画面の「タイトルへ」ボタンに機能を登録
        if (_ui != null && _ui.BackToTitleButton != null)
        {
            _ui.BackToTitleButton.onClick.RemoveAllListeners(); // ゲーム2回目以降の重複防止
            _ui.BackToTitleButton.onClick.AddListener(() => ReturnToTitleAsync().Forget());
        }

        UpdateScoreUI();
    }

    private async UniTaskVoid FadePanelAsync(float startAlpha, float endAlpha, CancellationToken token, float duration = 1f)
    {
        if (_fadePanel == null) return;

        try
        {
            _fadePanel.transform.SetAsLastSibling();
            _fadePanel.blocksRaycasts = true;
            _fadePanel.alpha = startAlpha;

            await UniTask.Delay(100, cancellationToken: token); // 少し待ってから開ける

            float time = 0;

            while (time < duration)
            {
                if (_fadePanel == null) return;

                time += Time.deltaTime;
                _fadePanel.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
                await UniTask.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合は正常な動作なのでログを出さずに終了
            return;
        }
        catch (Exception e)
        {
            Debug.LogError($"FadeAsync error: {e.Message}");
        }
        finally
        {
            if (_fadePanel != null)
            {
                _fadePanel.alpha = endAlpha;
                _fadePanel.blocksRaycasts = false; // ここから操作解禁
            }
        }
    }

    private async UniTaskVoid ReturnToTitleAsync()
    {
        if (_isTransitioning) return; // 二重遷移防止
        _isTransitioning = true;

        CancelGameLoop();

        // フェードアウト
        FadePanelAsync(0f, 1f, CancellationToken.None).Forget();

        try
        {
            await SceneManager.LoadSceneAsync(_titleSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load Title Scene: {e.Message}");
        }
    }

    private async UniTaskVoid GameLoopAsync(CancellationToken token)
    {
        _currentPlayerColor = StoneColor.Black;
        if (_ui != null) _ui.UpdateTurn(_currentPlayerColor);
        int consecutivePasses = 0;

        try
        {

            while (!token.IsCancellationRequested)
            {
                StoneInventory activeInventory = (_currentPlayerColor == StoneColor.Black) ? _blackInventory : _whiteInventory;
                _selectorUI.SwitchInventory(activeInventory);
                UpdateTurnState();

                // パス判定
                if (!ReversiRules.HasValidMove(_board, _currentPlayerColor, activeInventory))
                {
                    GameAudioManager.Instance.PlayPass();

                    consecutivePasses++;
                    if (_ui != null)
                    {
                        _ui.ShowThinking(false);
                        await _ui.ShowPassNotice(_currentPlayerColor);
                    }

                    if (consecutivePasses >= 2) break;

                    SwitchTurn();
                    continue;
                }

                consecutivePasses = 0;

                IPlayer activePlayer = (_currentPlayerColor == StoneColor.Black) ? _playerBlack : _playerWhite;
                bool isCpuTurn = !(activePlayer is HumanPlayer);

                if (isCpuTurn && _ui != null) _ui.ShowThinking(true);

                // 思考・入力待機
                // キャンセルトークンを渡すことで、ReturnToTitleが呼ばれたら即座に例外を発生させられる
                PlayerMove move = await activePlayer.DecideMoveAsync(_board, _currentPlayerColor, activeInventory, token);

                if (isCpuTurn && _ui != null) _ui.ShowThinking(false);

                // ロジックの適用
                if (ReversiRules.IsValidMove(_board, move))
                {
                    // データ上の盤面更新
                    MoveResult result = ReversiRules.ApplyMove(_board, move);

                    // データ上のインベントリ消費 (アニメーションを待たずに即座に減らす)
                    if (activePlayer is CPUPlayer)
                    {
                        activeInventory.LastSelected = move.Type;
                    }
                    _selectorUI.ConsumeCurrentSelection();

                    // 画面の演出を待機
                    await _view.AnimateMoveResultAsync(_board, result, token);

                    UpdateScoreUI();
                }
                else
                {
                    GameAudioManager.Instance.PlayInvalid();
                    continue; // やり直し
                }

                SwitchTurn();
            }

            // ゲーム終了
            if (!token.IsCancellationRequested)
            {
                await ShowResultAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("タイトルに戻るなどの理由で、ゲームループがキャンセルされました");
        }
    }

    private void SwitchTurn()
    {
        _currentPlayerColor = _currentPlayerColor.GetOpposite();
        if (_ui != null) _ui.UpdateTurn(_currentPlayerColor);
    }

    private void UpdateTurnState()
    {
        IPlayer activePlayer = (_currentPlayerColor == StoneColor.Black) ? _playerBlack : _playerWhite;
        _selectorUI.SetInteractable(activePlayer is HumanPlayer);
    }

    private void UpdateScoreUI()
    {
        if (_ui == null) return;
        int black = 0, white = 0;
        CountStones(out black, out white);
        _ui.UpdateScore(black, white);
    }

    private void CountStones(out int black, out int white)
    {
        black = white = 0;
        for (int y = 0; y < _board.Height; y++)
        {
            for (int x = 0; x < _board.Width; x++)
            {
                var color = _board.GetCell(x, y).Color;
                if (color == StoneColor.Black) black++;
                else if (color == StoneColor.White) white++;
            }
        }
    }

    private async UniTask ShowResultAsync(CancellationToken token)
    {
        int black = 0, white = 0;
        CountStones(out black, out white);

        // 最後の石が置かれて、一瞬の間を作る
        await UniTask.Delay(1000, cancellationToken: token);

        if (_ui != null)
        {
            await _ui.ShowResultAsync(black, white);
        }
    }

    #region Debug

    public void DebugButton()
    {
        CancellationToken token = _gameLoopCts.Token;
        ShowResultAsync(token).Forget();
    }

    #endregion
}
