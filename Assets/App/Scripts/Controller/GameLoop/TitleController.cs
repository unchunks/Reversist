using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
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
    private int _selectedDifficulty = 1;
    private GameSettings.PlayerSide _selectedSide = GameSettings.PlayerSide.Black;


    private void Start()
    {
        if (GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlayBGM(GameAudioManager.BgmType.Title);
        }

        FadeIn().Forget();

        _mainMenuGroup.alpha = 1;
        _mainMenuGroup.blocksRaycasts = true;

        _difficultyGroup.alpha = 0;
        _difficultyGroup.blocksRaycasts = false;

        InitializeDropdowns();
        SetupEvents();
    }

    private void InitializeDropdowns()
    {
        // --- 難易度ドロップダウン ---
        if (_difficultyDropdown != null)
        {
            _difficultyDropdown.ClearOptions();
            // 思考時間的に深さ6が限界
            List<string> diffOptions = new List<string>
            {
                "Lv.1",
                "Lv.2",
                "Lv.3",
                "Lv.4",
                "Lv.5",
                "Lv.6"
            };
            _difficultyDropdown.AddOptions(diffOptions);

            _difficultyDropdown.value = 0;
            _selectedDifficulty = 1;
        }

        // --- 陣営ドロップダウン ---
        if (_sideDropdown != null)
        {
            _sideDropdown.ClearOptions();
            List<string> sideOptions = new List<string>
            {
                "BLACK",
                "WHITE",
                "RANDOM"
            };
            _sideDropdown.AddOptions(sideOptions);

            _sideDropdown.value = 0;
            _selectedSide = GameSettings.PlayerSide.Black;
        }
    }

    private void SetupEvents()
    {
        // Main Menu
        _btnPvP.onClick.AddListener(() =>
        {
            GameAudioManager.Instance.PlayUIClick();
            StartGame(GameSettings.GameMode.PvP);
        });
        _btnPvE.onClick.AddListener(() =>
        {
            GameAudioManager.Instance.PlayUIClick();
            ShowDifficultySelect();
        });
        _btnQuit.onClick.AddListener(() =>
        {
            GameAudioManager.Instance.PlayUIClick();
            QuitGame();
        });

        // PvE Settings - Difficulty Dropdown
        if (_difficultyDropdown != null)
        {
            _difficultyDropdown.onValueChanged.AddListener((int index) =>
            {
                GameAudioManager.Instance.PlayUIClick();
                OnDifficultyDropdownChanged(index);
            });
        }

        // PvE Settings - Side Dropdown
        if (_sideDropdown != null)
        {
            _sideDropdown.onValueChanged.AddListener((int index) =>
            {
                GameAudioManager.Instance.PlayUIClick();
                OnSideDropdownChanged(index);
            });
        }

        // Start & Back
        _btnStartPvE.onClick.AddListener(() =>
        {
            GameAudioManager.Instance.PlayUIClick();
            StartGame(GameSettings.GameMode.PvE, _selectedDifficulty, _selectedSide);
        });
        _btnBack.onClick.AddListener(() =>
        {
            GameAudioManager.Instance.PlayUIClick();
            BackToMain();
        });
    }

    private void OnDifficultyDropdownChanged(int index)
    {
        // 難易度は1から始まるので+1
        _selectedDifficulty = index + 1;
    }

    private void OnSideDropdownChanged(int index)
    {
        switch (index)
        {
            case 0: _selectedSide = GameSettings.PlayerSide.Black; break;
            case 1: _selectedSide = GameSettings.PlayerSide.White; break;
            case 2: _selectedSide = GameSettings.PlayerSide.Random; break;
        }
    }

    private void ShowDifficultySelect()
    {
        SwitchMenu(_mainMenuGroup, _difficultyGroup).Forget();
    }

    private void BackToMain()
    {
        SwitchMenu(_difficultyGroup, _mainMenuGroup).Forget();
    }

    private async UniTaskVoid SwitchMenu(CanvasGroup from, CanvasGroup to)
    {
        from.blocksRaycasts = false;
        float duration = 0.3f;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            from.alpha = 1f - t;
            to.alpha = t;
            await UniTask.Yield();
        }
        from.alpha = 0;
        to.alpha = 1;
        to.blocksRaycasts = true;
    }

    private void StartGame(GameSettings.GameMode mode, int difficulty = 3, GameSettings.PlayerSide side = GameSettings.PlayerSide.Black)
    {
        GameSettings.Mode = mode;
        GameSettings.AiDifficulty = difficulty;
        GameSettings.Side = side;

        LoadGameSceneAsync().Forget();
    }

    private async UniTaskVoid LoadGameSceneAsync()
    {
        _mainMenuGroup.blocksRaycasts = false;
        _difficultyGroup.blocksRaycasts = false;

        try
        {
            await FadeOut();

            await SceneManager.LoadSceneAsync(_gameSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError($"Scene Load Failed: {e.Message}");
            if (_fadePanel != null) _fadePanel.blocksRaycasts = false;
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

    private async UniTask FadeIn()
    {
        if (_fadePanel != null)
        {
            // フェードパネルをCanvas内の最前面（描画順最後）に移動
            // これにより、他の全てのUIの上に覆いかぶさる
            _fadePanel.transform.SetAsLastSibling();

            _fadePanel.alpha = 1f;
            _fadePanel.blocksRaycasts = true;

            // フェード時間
            float duration = 1.0f;
            float time = 0;

            while (time < duration)
            {
                time += Time.deltaTime;
                _fadePanel.alpha = 1f - time / duration;
                await UniTask.Yield();
            }

            _fadePanel.alpha = 0f;
            _fadePanel.blocksRaycasts = false;
        }
        else
        {
            Debug.LogWarning("FadePanel is NOT assigned! Jumping to scene directly.");
        }
    }

    private async UniTask FadeOut()
    {
        if (_fadePanel != null)
        {
            // フェードパネルをCanvas内の最前面（描画順最後）に移動
            // これにより、他の全てのUIの上に覆いかぶさる
            _fadePanel.transform.SetAsLastSibling();

            _fadePanel.alpha = 0f;
            _fadePanel.blocksRaycasts = true;

            // フェード時間
            float duration = 1.0f;
            float time = 0;

            while (time < duration)
            {
                time += Time.deltaTime;
                _fadePanel.alpha = time / duration;
                await UniTask.Yield();
            }

            _fadePanel.alpha = 1f;
        }
        else
        {
            Debug.LogWarning("FadePanel is NOT assigned! Jumping to scene directly.");
        }
    }
}
