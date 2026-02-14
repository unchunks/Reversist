using Cysharp.Threading.Tasks;
using System;
using System.Threading;
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
    private CancellationTokenSource _cts;

    private IPlayer _playerBlack;
    private IPlayer _playerWhite;

    private StoneInventory _blackInventory;
    private StoneInventory _whiteInventory;

    void Start()
    {
        _cts = new CancellationTokenSource();

        // 初期化と同時にフェードインを開始
        InitializeGame();

        // パネルを最前面にしてフェードイン開始
        if (_fadePanel != null)
        {
            _fadePanel.transform.SetAsLastSibling();
            _fadePanel.alpha = 1f; // 最初は真っ暗
            _fadePanel.blocksRaycasts = true; // 操作ブロック
            FadeInAsync().Forget();
        }

        GameLoop(_cts.Token).Forget();
    }

    private async UniTaskVoid FadeInAsync()
    {
        if (_fadePanel == null) return;

        await UniTask.Delay(500); // 少し待ってから開ける（演出）

        float duration = 1.0f;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            _fadePanel.alpha = 1f - (time / duration); // 1 -> 0
            await UniTask.Yield();
        }

        _fadePanel.alpha = 0f;
        _fadePanel.blocksRaycasts = false; // 操作解禁
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private void InitializeGame()
    {
        // BGM切り替え（クロスフェード）
        GameAudioManager.Instance.PlayBGM(GameAudioManager.BgmType.MainGame);
        GameAudioManager.Instance.PlayGameStart(); // 開始SE

        _board = new BoardState();
        _view.Initialize(_board);

        _blackInventory = new StoneInventory();
        _whiteInventory = new StoneInventory();

        // プレイヤー設定の読み込み
        if (GameSettings.Mode == GameSettings.GameMode.PvP)
        {
            // PvP: 両方Human
            _playerBlack = new HumanPlayer(_view, _selectorUI);
            _playerWhite = new HumanPlayer(_view, _selectorUI);

            // UI初期化 (黒番開始なので黒インベントリ)
            _selectorUI.Initialize(_blackInventory);
        }
        else
        {
            // PvE: 設定に応じて割り振り
            GameSettings.PlayerSide playerSide = GameSettings.Side;

            // Randomならここで決定
            if (playerSide == GameSettings.PlayerSide.Random)
            {
                playerSide = (UnityEngine.Random.Range(0, 2) == 0) ? GameSettings.PlayerSide.Black : GameSettings.PlayerSide.White;
                Debug.Log($"Random Side Selected: Player is {playerSide}");
            }

            int depth = GameSettings.AiDifficulty;
            IPlayer human = new HumanPlayer(_view, _selectorUI);
            IPlayer cpu = new CPUPlayer(new LogicAI(depth));

            if (playerSide == GameSettings.PlayerSide.Black)
            {
                // プレイヤーが黒（先攻）
                _playerBlack = human;
                _playerWhite = cpu;

                // UIはプレイヤー用（黒）で初期化
                _selectorUI.Initialize(_blackInventory);
            }
            else
            {
                // プレイヤーが白（後攻）
                _playerBlack = cpu;
                _playerWhite = human;

                // UIはプレイヤー用（白）のインベントリを表示すべきだが、
                // ゲーム開始時は必ず「黒番」から始まるため、
                // GameLoopの冒頭で _selectorUI.SwitchInventory(_blackInventory) が呼ばれてしまう。
                // したがって、ここではどちらで初期化しても一瞬で上書きされるが、
                // 「自分の持ち物」を確認できるように白で初期化しておくのが親切か。
                // ただし、CPUターン中はUIロックがかかるので、結局操作はできない。
                _selectorUI.Initialize(_whiteInventory);
            }
        }

        // リザルト画面の「タイトルへ」ボタンに機能を登録
        if (_ui != null && _ui.BackToTitleButton != null)
        {
            _ui.BackToTitleButton.onClick.RemoveAllListeners(); // 重複防止
            _ui.BackToTitleButton.onClick.AddListener(() => ReturnToTitleAsync().Forget());
        }

        UpdateGameUI();
    }

    /// <summary>
    /// タイトルへ戻る処理（フェードアウト付き）
    /// </summary>
    private async UniTaskVoid ReturnToTitleAsync()
    {
        // 二重押し防止
        if (_fadePanel != null && _fadePanel.blocksRaycasts) return;

        Debug.Log("Returning to Title...");

        // フェードアウト
        if (_fadePanel != null)
        {
            _fadePanel.transform.SetAsLastSibling(); // 最前面へ
            _fadePanel.blocksRaycasts = true; // 入力ブロック

            float duration = 1.0f;
            float time = 0;

            while (time < duration)
            {
                time += Time.deltaTime;
                _fadePanel.alpha = time / duration; // 0 -> 1
                await UniTask.Yield();
            }
            _fadePanel.alpha = 1f;
        }

        // シーンロード
        try
        {
            await SceneManager.LoadSceneAsync(_titleSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load Title Scene: {e.Message}");
        }
    }

    private async UniTaskVoid GameLoop(CancellationToken token)
    {
        _currentPlayerColor = StoneColor.Black;
        int consecutivePasses = 0;

        StoneInventory activeInventory = _blackInventory;
        _selectorUI.SwitchInventory(activeInventory);
        UpdateTurnState();
        UpdateGameUI();

        try
        {
            while (true)
            {
                activeInventory = (_currentPlayerColor == StoneColor.Black) ? _blackInventory : _whiteInventory;

                bool hasMove = ReversiRules.HasValidMove(_board, _currentPlayerColor, activeInventory);

                if (!hasMove)
                {
                    Debug.Log($"{_currentPlayerColor} has no moves! PASS.");
                    if (_ui != null) _ui.ShowThinking(false);

                    if (_ui != null) await _ui.ShowPassNotice(_currentPlayerColor);

                    consecutivePasses++;

                    if (consecutivePasses >= 2)
                    {
                        Debug.Log("Both players passed. Game Over.");
                        break;
                    }

                    _currentPlayerColor = (_currentPlayerColor == StoneColor.Black) ? StoneColor.White : StoneColor.Black;
                    if (_ui != null) _ui.UpdateTurn(_currentPlayerColor);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                consecutivePasses = 0;

                _selectorUI.SwitchInventory(activeInventory);
                UpdateTurnState();

                IPlayer activePlayer = (_currentPlayerColor == StoneColor.Black) ? _playerBlack : _playerWhite;

                // 相手がCPUの場合、思考中UIを表示
                // (IsHuman判定はUpdateTurnStateでも使っているので、もっと綺麗に共通化できるが、ここでは型チェックを行う)
                bool isCpuTurn = !(activePlayer is HumanPlayer);

                if (isCpuTurn && _ui != null)
                {
                    _ui.ShowThinking(true);
                }

                // 思考・入力待機
                PlayerMove move = await activePlayer.DecideMoveAsync(_board, _currentPlayerColor, activeInventory, token);

                // 思考終了でUIを消す
                if (isCpuTurn && _ui != null)
                {
                    _ui.ShowThinking(false);
                }

                if (ReversiRules.IsValidMove(_board, move))
                {
                    // 戻り値(MoveResult)を受け取る
                    MoveResult result = ReversiRules.ApplyMove(_board, move);

                    // アニメーション付きで更新し、完了を待つ
                    // UpdateBoardではなくAnimateMoveResultAsyncを呼ぶ
                    await _view.AnimateMoveResultAsync(_board, result);

                    UpdateGameUI();

                    if (activePlayer is CPUPlayer)
                    {
                        activeInventory.LastSelected = move.Type;
                    }
                    _selectorUI.ConsumeCurrentSelection();
                }
                else
                {
                    Debug.LogWarning("Invalid Move! Force Retry or Pass logic here.");
                    continue;
                }

                _currentPlayerColor = (_currentPlayerColor == StoneColor.Black) ? StoneColor.White : StoneColor.Black;
                if (_ui != null) _ui.UpdateTurn(_currentPlayerColor);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            await ShowResult(token);
        }
        catch (OperationCanceledException) { }
    }

    private void UpdateTurnState()
    {
        IPlayer activePlayer = (_currentPlayerColor == StoneColor.Black) ? _playerBlack : _playerWhite;
        if (activePlayer is HumanPlayer) _selectorUI.SetInteractable(true);
        else _selectorUI.SetInteractable(false);
    }

    private void UpdateGameUI()
    {
        if (_ui == null) return;
        _ui.UpdateTurn(_currentPlayerColor);

        int black = 0;
        int white = 0;
        CountStones(out black, out white);
        _ui.UpdateScore(black, white);
    }

    private void CountStones(out int black, out int white)
    {
        black = 0;
        white = 0;
        for (int y = 0; y < _board.Height; y++)
        {
            for (int x = 0; x < _board.Width; x++)
            {
                var cell = _board.GetCell(x, y);
                if (cell.Color == StoneColor.Black) black++;
                else if (cell.Color == StoneColor.White) white++;
            }
        }
    }

    private async UniTask ShowResult(CancellationToken token)
    {
        // --- ゲーム終了処理 ---
        Debug.Log("Starting Game Over Sequence...");

        // 最終集計
        int black = 0, white = 0;
        CountStones(out black, out white);

        // 最後の石が置かれて、一瞬の間を作る
        await UniTask.Delay(1000, cancellationToken: token);

        // アニメーション付きリザルト表示
        if (_ui != null)
        {
            await _ui.ShowResultAsync(black, white);
        }
    }

    #region Debug Button

    // これをインスペクターの Button OnClick に登録する
    public void DebugButton()
    {
        CancellationToken token = _cts.Token;
        ShowResult(token).Forget();
    }

    #endregion
}
