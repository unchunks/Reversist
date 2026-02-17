using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class StoneTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private RectTransform _backgroundRect;

    [Header("Settings")]
    [SerializeField] private float _padding = 10f; // 画面端からの最小余白
    [SerializeField] private float _offsetFromButton = 20f; // ボタンから離す距離

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
        _parentCanvas = GetComponentInParent<Canvas>();

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        Hide();
    }

    public void Show(StoneData data, RectTransform targetButtonRect)
    {
        _titleText.text = data.Title;
        _titleText.color = data.ThemeColor;
        _descriptionText.text = data.Description;

        // テキスト更新直後の矩形サイズ
        LayoutRebuilder.ForceRebuildLayoutImmediate(_backgroundRect);

        // ボタンの四隅のワールド座標を取得
        Vector3[] buttonCorners = new Vector3[4];
        targetButtonRect.GetWorldCorners(buttonCorners);

        // corners[0]=左下, [1]=左上, [2]=右上, [3]=右下
        Vector3 buttonRightCenter = (buttonCorners[2] + buttonCorners[3]) / 2f;
        Vector3 buttonLeftCenter = (buttonCorners[0] + buttonCorners[1]) / 2f;

        // 基本はボタンの「右側」に配置する
        _rectTransform.pivot = new Vector2(0f, 0.5f);
        _rectTransform.position = buttonRightCenter + new Vector3(_offsetFromButton, 0, 0);

        // 画面右端からはみ出すかのチェック
        Vector3[] tooltipCorners = new Vector3[4];
        _backgroundRect.GetWorldCorners(tooltipCorners);
        Rect screenRect = Screen.safeArea;

        // もし右端が画面外(＋パディング)を超えるなら、フリップしてボタンの「左側」に配置する
        if (tooltipCorners[2].x > screenRect.xMax - _padding)
        {
            _rectTransform.pivot = new Vector2(1f, 0.5f); // ピボットを右端に変更
            _rectTransform.position = buttonLeftCenter - new Vector3(_offsetFromButton, 0, 0);
        }

        // Y軸の画面外補正（上下の見切れ対策）
        // ピボット変更後の正確な座標を再取得
        _backgroundRect.GetWorldCorners(tooltipCorners);
        float shiftY = 0f;

        if (tooltipCorners[1].y > screenRect.yMax - _padding) // 上にはみ出た
        {
            shiftY = (screenRect.yMax - _padding) - tooltipCorners[1].y;
        }
        else if (tooltipCorners[0].y < screenRect.y + _padding) // 下にはみ出た
        {
            shiftY = (screenRect.y + _padding) - tooltipCorners[0].y;
        }

        // 上下の押し戻しを適用
        _rectTransform.position += new Vector3(0, shiftY, 0);

        _canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        _canvasGroup.alpha = 0f;
    }
}
