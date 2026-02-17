using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkitを使用してゲーム全体の設定画面を管理するクラス
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GlobalSettingsUI : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _container;

    // UI Elements
    private Slider _masterSlider;
    private Slider _bgmSlider;
    private Slider _seSlider;
    private Button _closeButton;
    private Button _quitButton;

    private bool _isShown = false;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        // ルート要素取得
        var root = _uiDocument.rootVisualElement;
        if (root == null) return;

        // UXML内の要素を名前で検索 (Query)
        _container = root.Q<VisualElement>("Container");
        _masterSlider = root.Q<Slider>("SliderMaster");
        _bgmSlider = root.Q<Slider>("SliderBGM");
        _seSlider = root.Q<Slider>("SliderSE");
        _closeButton = root.Q<Button>("BtnClose");
        _quitButton = root.Q<Button>("BtnQuit");

        // イベント登録
        if (_closeButton != null) _closeButton.clicked += () => Toggle();
        if (_quitButton != null) _quitButton.clicked += QuitGame;

        if (_masterSlider != null)
        {
            _masterSlider.RegisterValueChangedCallback(evt =>
            {
                if (GameAudioManager.Instance != null)
                    GameAudioManager.Instance.SetMasterVolume(evt.newValue);
            });
        }

        if (_bgmSlider != null)
        {
            _bgmSlider.RegisterValueChangedCallback(evt =>
            {
                if (GameAudioManager.Instance != null)
                    GameAudioManager.Instance.SetBGMVolume(evt.newValue);
            });
        }

        if (_seSlider != null)
        {
            _seSlider.RegisterValueChangedCallback(evt =>
            {
                if (GameAudioManager.Instance != null)
                    GameAudioManager.Instance.SetSEVolume(evt.newValue);
            });
        }

        // 初期化時は非表示
        if (_container != null)
        {
            _container.RemoveFromClassList("shown");
            _container.pickingMode = PickingMode.Position;
        }
    }

    // 表示時に値を同期
    private void SyncValues()
    {
        if (GameAudioManager.Instance == null) return;

        if (_masterSlider != null)
            _masterSlider.value = GameAudioManager.Instance.CurrentMasterVolume;

        if (_bgmSlider != null)
            _bgmSlider.value = GameAudioManager.Instance.CurrentBGMVolume;

        if (_seSlider != null)
            _seSlider.value = GameAudioManager.Instance.CurrentSEVolume;
    }

    public void Show()
    {
        if (_isShown) return;
        _isShown = true;

        SyncValues();

        // CSSクラスを付与してフェードインアニメーション開始
        if (_container != null)
        {
            _container.AddToClassList("shown");
        }

        // SE
        if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayUIHover();
    }

    public void Hide()
    {
        if (!_isShown) return;
        _isShown = false;

        // 設定保存
        if (GameAudioManager.Instance != null) GameAudioManager.Instance.SaveSettings();

        // CSSクラスを削除してフェードアウト
        if (_container != null)
        {
            _container.RemoveFromClassList("shown");
        }

        // SE
        if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayUIClick();
    }

    public void Toggle()
    {
        if (_isShown) Hide();
        else Show();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
