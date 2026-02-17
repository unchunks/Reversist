using UnityEngine;

/// <summary>
/// ゲーム全体で共通して使用されるUI（設定画面）を管理するシングルトンマネージャー
/// </summary>
public class GlobalUIManager : MonoBehaviour
{
    public static GlobalUIManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GlobalSettingsUI _settingsUIPrefab;

    private GlobalSettingsUI _settingsUIInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 設定画面の実体を生成（非表示状態で）
        if (_settingsUIPrefab != null)
        {
            _settingsUIInstance = Instantiate(_settingsUIPrefab, transform);
        }
        else
        {
            Debug.LogWarning("Settings UI Prefab not assigned in GlobalUIManager!");
        }
    }

    /// <summary>
    /// 設定画面を開く（または閉じる）
    /// </summary>
    public void ToggleSettings()
    {
        if (_settingsUIInstance != null)
        {
            _settingsUIInstance.Toggle();
        }
    }
}
