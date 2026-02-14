using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

public class GameUIView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _turnText;
    [SerializeField] private TextMeshProUGUI _blackScoreText;
    [SerializeField] private TextMeshProUGUI _whiteScoreText;

    [Header("Icon References")]
    [SerializeField] private SVGImage _turnPlayerIcon; // ターン表示用のアイコン
    [SerializeField] private Sprite _blackIcon;     // Inspectorでセット
    [SerializeField] private Sprite _whiteIcon;     // Inspectorでセット

    [Header("Result References")]
    [SerializeField] private CanvasGroup _resultPanel;
    [SerializeField] private RectTransform _resultWindowRect;
    [SerializeField] private CanvasGroup _resultContentGroup;
    [SerializeField] private SVGImage _winnerIcon;
    [SerializeField] private TextMeshProUGUI _winnerText;
    [SerializeField] private TextMeshProUGUI _resultScoreText;

    [SerializeField] private Image _resultWindowImage;
    [SerializeField] private Material _blackWinMaterial; // 黒勝利用
    [SerializeField] private Material _whiteWinMaterial; // 白勝利用
    [SerializeField] private Material _drawMaterial;     // 引き分け用

    [SerializeField] private Button _backToTitleButton;
    public Button BackToTitleButton => _backToTitleButton;

    [Header("Thinking UI")]
    [SerializeField] private CanvasGroup _thinkingPanel;
    [SerializeField] private TextMeshProUGUI _thinkingText;

    [Header("Team Color Settings (For Dark Background)")]
    // 黒背景（ターン表示など）で見やすい色
    [SerializeField] private Color _blackTeamColor = new Color(1.0f, 0.2f, 1.0f); // 明るいマゼンタ
    [SerializeField] private Color _whiteTeamColor = new Color(0.0f, 1.0f, 1.0f); // シアン

    [Header("Result Text Color Settings (For Colored Background)")]
    // リザルト背景（マゼンタ/シアン）の上で見やすい色
    [SerializeField] private Color _blackWinTextColor = Color.white; // マゼンタ背景には白文字
    [SerializeField] private Color _whiteWinTextColor = new Color(0.1f, 0.1f, 0.1f); // シアン背景には黒文字

    [SerializeField] private Color _inactiveColor = Color.gray;

    private CancellationTokenSource _thinkingCts;

    private void Awake()
    {
        if (_resultPanel != null)
        {
            _resultPanel.alpha = 0f;
            _resultPanel.blocksRaycasts = false;

            if (_resultWindowRect != null)
                _resultWindowRect.localScale = new Vector3(0, 0.02f, 1);

            if (_resultContentGroup != null)
                _resultContentGroup.alpha = 0f;
        }

        if (_thinkingPanel != null)
        {
            _thinkingPanel.alpha = 0f;
            _thinkingPanel.blocksRaycasts = false;
        }
    }

    public void UpdateTurn(StoneColor currentTurn)
    {
        if (_turnText == null) return;

        if (currentTurn == StoneColor.Black)
        {
            _turnText.text = "TURN: ";
            _turnPlayerIcon.sprite = _blackIcon;
            _turnPlayerIcon.color = _blackTeamColor;
            _turnText.color = _blackTeamColor;
        }
        else
        {
            _turnText.text = "TURN: ";
            _turnPlayerIcon.sprite = _whiteIcon;
            _turnPlayerIcon.color = _whiteTeamColor;
            _turnText.color = _whiteTeamColor;
        }
    }

    public void UpdateScore(int black, int white)
    {
        if (_blackScoreText != null)
        {
            _blackScoreText.text = $"{black:D2}";
            _blackScoreText.color = (black >= white) ? _blackTeamColor : Color.gray;
        }
        if (_whiteScoreText != null)
        {
            _whiteScoreText.text = $"{white:D2}";
            _whiteScoreText.color = (white >= black) ? _whiteTeamColor : Color.gray;
        }
    }

    public async UniTask ShowPassNotice(StoneColor passPlayer)
    {
        if (_turnText == null) return;
        string originalText = _turnText.text;
        Color originalColor = _turnText.color;

        _turnText.text = $"PASS: ";
        _turnText.color = Color.red;

        for (int i = 0; i < 3; i++)
        {
            _turnText.enabled = false;
            await UniTask.Delay(100);
            _turnText.enabled = true;
            await UniTask.Delay(100);
        }
        await UniTask.Delay(500);

        _turnText.text = originalText;
        _turnText.color = originalColor;
    }

    public async UniTask ShowResultAsync(int blackCount, int whiteCount)
    {
        ShowThinking(false);

        if (_resultPanel == null || _resultWindowRect == null) return;

        string winnerString;
        Color winnerColor;
        Material targetMaterial = null; // 適用するマテリアル

        _winnerIcon.gameObject.SetActive(true);

        // 勝敗判定とマテリアル選択
        if (blackCount > whiteCount)
        {
            winnerString = "WINNER:";
            winnerColor = _blackWinTextColor;
            targetMaterial = _blackWinMaterial;
            _winnerIcon.sprite = _blackIcon;
            _winnerIcon.color = _blackTeamColor;
        }
        else if (whiteCount > blackCount)
        {
            winnerString = "WINNER:";
            winnerColor = _whiteWinTextColor;
            targetMaterial = _whiteWinMaterial;
            _winnerIcon.sprite = _whiteIcon;
            _winnerIcon.color = _blackTeamColor;
        }
        else
        {
            winnerString = "DRAW GAME";
            winnerColor = Color.gray;
            targetMaterial = _drawMaterial;
            _winnerIcon.gameObject.SetActive(false);
        }

        if (_winnerText != null)
        {
            _winnerText.text = winnerString;
            _winnerText.color = winnerColor;
        }

        if (_resultWindowImage != null && targetMaterial != null)
        {
            _resultWindowImage.material = targetMaterial;
        }

        _resultPanel.blocksRaycasts = true;
        _resultWindowRect.localScale = new Vector3(0f, 0.02f, 1f);
        if (_resultContentGroup != null) _resultContentGroup.alpha = 0f;

        // ★追加: 結果BGM
        var bgmType = (winnerString == "DRAW GAME") ? GameAudioManager.BgmType.Lose : GameAudioManager.BgmType.Win;

        // Step 1: Background & Line
        float duration = 0.3f;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            _resultPanel.alpha = Mathf.Lerp(0f, 1f, t);
            _resultWindowRect.localScale = new Vector3(ease, 0.02f, 1f);

            await UniTask.Yield();
        }
        _resultWindowRect.localScale = new Vector3(1f, 0.02f, 1f);

        await UniTask.Delay(50);

        // Step 2: Open Window
        time = 0;
        duration = 0.4f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float ease = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            float currentY = Mathf.Lerp(0.02f, 1f, ease);
            _resultWindowRect.localScale = new Vector3(1f, currentY, 1f);
            await UniTask.Yield();
        }
        _resultWindowRect.localScale = Vector3.one;

        // Step 3: Content & Score
        if (_resultContentGroup != null)
        {
            time = 0;
            duration = 0.2f;
            while (time < duration)
            {
                time += Time.deltaTime;
                _resultContentGroup.alpha = time / duration;
                await UniTask.Yield();
            }
            _resultContentGroup.alpha = 1f;
        }

        if (_resultScoreText != null)
        {
            time = 0;
            duration = 0.8f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - time / duration, 3f);
                int curB = (int)Mathf.Lerp(0, blackCount, t);
                int curW = (int)Mathf.Lerp(0, whiteCount, t);
                _resultScoreText.text = $"{curB:D2} - {curW:D2}";
                _resultScoreText.color = winnerColor;
                await UniTask.Yield();
            }
            _resultScoreText.text = $"{blackCount:D2} - {whiteCount:D2}";
        }
    }

    // --- Thinking UI Control ---

    public void ShowThinking(bool show)
    {
        if (_thinkingPanel == null) return;

        // 既存のアニメーションをキャンセル
        _thinkingCts?.Cancel();
        _thinkingCts?.Dispose();
        _thinkingCts = null;

        if (show)
        {
            _thinkingPanel.alpha = 1f;
            _thinkingCts = new CancellationTokenSource();
            AnimateThinkingText(_thinkingCts.Token).Forget();
        }
        else
        {
            _thinkingPanel.alpha = 0f;
        }
    }

    private async UniTaskVoid AnimateThinkingText(CancellationToken token)
    {
        if (_thinkingText == null) return;

        string baseText = "SYSTEM ANALYZING...";
        int length = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                _thinkingText.text = baseText.Substring(0, length);

                // 点滅演出も兼ねて色を少し変える
                float blink = Mathf.PingPong(Time.time * 5f, 0.5f) + 0.5f;
                _thinkingText.color = new Color(1f, 0f, 0f, blink);

                await UniTask.Delay(100, cancellationToken: token);

                length = (length + 1) % (baseText.Length + 1);
            }
        }
        catch (System.OperationCanceledException)
        {
            // キャンセル時は何もしない
        }
    }
}
