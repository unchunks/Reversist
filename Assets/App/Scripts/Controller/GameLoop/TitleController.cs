using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup _mainMenuGroup;
    [SerializeField] private CanvasGroup _difficultyGroup;
    [SerializeField] private CanvasGroup _fadePanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button _btnPvP;
    [SerializeField] private Button _btnPvE;
    [SerializeField] private Button _btnQuit;

    [Header("PvE Settings UI")]
    [SerializeField] private TMP_Dropdown _difficultyDropdown;
    [SerializeField] private TMP_Dropdown _sideDropdown;

    [Header("Navigation")]
    [SerializeField] private Button _btnStartPvE;
    [SerializeField] private Button _btnBack;

    [Header("Scene Settings")]
    [SerializeField] private string _gameSceneName = "ReversiScene";

    // 選択中の設定
    private const int DEFAULT_DIFFICULTY = 1;
    private int _selectedDifficulty = DEFAULT_DIFFICULTY;
    private GameSettings.PlayerSide _selectedSide = GameSettings.PlayerSide.Black;

    private CancellationTokenSource _cts;
    private bool _isTransitioning = false; // UIの二重操作防止フラグ

    private void Start()
    {
        _cts = new CancellationTokenSource();

        if (GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlayBGM(GameAudioManager.BgmType.Title);
        }

        _mainMenuGroup.alpha = 1;
        _mainMenuGroup.blocksRaycasts = true;

        _difficultyGroup.alpha = 0;
        _difficultyGroup.blocksRaycasts = false;

        InitializeDropdowns();
        SetupEvents();

        FadePanelAsync(1f, 0f, _cts.Token).Forget();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private void InitializeDropdowns()
    {
        if (_difficultyDropdown != null)
        {
            _difficultyDropdown.ClearOptions();
            // 思考時間的に深さ6が限界
            _difficultyDropdown.AddOptions(new List<string> { "Lv.1", "Lv.2", "Lv.3", "Lv.4", "Lv.5", "Lv.6" });
            _difficultyDropdown.value = DEFAULT_DIFFICULTY - 1;
            _selectedDifficulty = DEFAULT_DIFFICULTY;
        }

        if (_sideDropdown != null)
        {
            _sideDropdown.ClearOptions();
            _sideDropdown.AddOptions(new List<string> { "黒（先手）", "白（後手）", "ランダム" });
            _sideDropdown.value = 0;
            _selectedSide = GameSettings.PlayerSide.Black;
        }
    }

    private void SetupEvents()
    {
        // メイン画面
        BindButton(_btnPvP, () => StartGameAsync(GameSettings.GameMode.PvP).Forget());
        BindButton(_btnPvE, () => SwitchMenuAsync(_mainMenuGroup, _difficultyGroup).Forget());
        BindButton(_btnQuit, QuitGame);

        // PvE画面
        BindButton(_btnStartPvE, () => StartGameAsync(GameSettings.GameMode.PvE, _selectedDifficulty, _selectedSide).Forget());
        BindButton(_btnBack, () => SwitchMenuAsync(_difficultyGroup, _mainMenuGroup).Forget());

        if (_difficultyDropdown != null)
        {
            _difficultyDropdown.onValueChanged.AddListener((index) =>
            {
                if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayUIClick();
                _selectedDifficulty = index + 1;
            });
        }

        if (_sideDropdown != null)
        {
            _sideDropdown.onValueChanged.AddListener((index) =>
            {
                if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayUIClick();
                _selectedSide = index switch
                {
                    0 => GameSettings.PlayerSide.Black,
                    1 => GameSettings.PlayerSide.White,
                    _ => GameSettings.PlayerSide.Random,
                };
            });
        }
    }

    /// <summary>
    /// ボタンに共通処理（SE再生と二重押し防止）を付与して登録する
    /// </summary>
    private void BindButton(Button btn, Action action)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() =>
        {
            if (_isTransitioning) return; // 遷移中は何もしない
            if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayUIClick();
            action.Invoke();
        });
    }

    private async UniTaskVoid SwitchMenuAsync(CanvasGroup from, CanvasGroup to)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        from.blocksRaycasts = false;
        to.blocksRaycasts = false;

        try
        {
            float duration = 0.3f;
            float time = 0;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;
                from.alpha = 1f - t;
                to.alpha = t;
                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Menu Switch Failed: {e.Message}");
        }
        finally
        {
            from.alpha = 0f;
            to.alpha = 1f;
            to.blocksRaycasts = true;
            _isTransitioning = false;
        }
    }

    private async UniTaskVoid StartGameAsync(GameSettings.GameMode mode, int difficulty = DEFAULT_DIFFICULTY, GameSettings.PlayerSide side = GameSettings.PlayerSide.Black)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        GameSettings.Mode = mode;
        GameSettings.AiDifficulty = difficulty;
        GameSettings.Side = side;

        _mainMenuGroup.blocksRaycasts = false;
        _difficultyGroup.blocksRaycasts = false;

        try
        {
            await FadePanelAsync(0f, 1f, _cts.Token);
            await SceneManager.LoadSceneAsync(_gameSceneName);
        }
        catch (OperationCanceledException)
        {
            // キャンセルの場合 = ゲームを終了したとして、UIのは放置
            Debug.Log("Scene Load Cancelled.");
        }
        catch (Exception e)
        {
            // 何らかの理由でシーンの読み込みに失敗した場合、UIを復旧させる
            Debug.LogError($"Scene Load Failed: {e.Message}");
            _isTransitioning = false;
            _mainMenuGroup.blocksRaycasts = true;
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    private async UniTask FadePanelAsync(float startAlpha, float endAlpha, CancellationToken token, float duration = 1f)
    {
        if (_fadePanel == null)
        {
            Debug.LogWarning("FadePanel is NOT assigned!");
            return;
        }

        _fadePanel.transform.SetAsLastSibling();
        _fadePanel.blocksRaycasts = true;

        try
        {
            float time = 0;
            while (time < duration)
            {
                time += Time.deltaTime;
                _fadePanel.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            _fadePanel.alpha = endAlpha;
        }
        finally
        {
            // FadeOutはRaycastブロックを維持し、FadeInは解除
            _fadePanel.blocksRaycasts = (endAlpha > 0.5f);
        }
    }
}
