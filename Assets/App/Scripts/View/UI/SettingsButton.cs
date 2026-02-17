using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingsButton : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (GlobalUIManager.Instance != null)
            {
                // SE
                if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayUIClick();

                // Toggle
                GlobalUIManager.Instance.ToggleSettings();
            }
            else
            {
                Debug.LogWarning("GlobalUIManager not found! Make sure to start from Title Scene.");
            }
        });
    }
}
