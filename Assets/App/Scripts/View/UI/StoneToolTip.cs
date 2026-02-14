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
    [SerializeField] private float _offsetX = 700f; // ボタンの基準位置（Left）からのオフセット
    [SerializeField] private float _padding = 10f; // 画面端からの余白

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        // 初期化時にレイキャストを無効化する
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        Hide();
    }

    public void Show(string title, string description, Vector3 targetWorldPos, Color themeColor)
    {
        _titleText.text = title;
        _titleText.color = themeColor;
        _descriptionText.text = description;

        // まずターゲット基準の理想位置に配置
        transform.position = targetWorldPos + Vector3.right * _offsetX;

        // コンテンツの内容量に合わせてサイズを確定させる（これがないと計算がズレる）
        LayoutRebuilder.ForceRebuildLayoutImmediate(_backgroundRect);

        // 画面内に収まるように位置補正
        KeepInScreen();

        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = false;
    }

    private void KeepInScreen()
    {
        // 自身の四隅のワールド座標を取得
        // corners[0]=左下, [1]=左上, [2]=右上, [3]=右下
        Vector3[] corners = new Vector3[4];
        _backgroundRect.GetWorldCorners(corners);

        Rect screenRect = Screen.safeArea;
        float shiftX = 0f;
        float shiftY = 0f;

        // --- 水平方向 (Horizontal) ---

        // 左にはみ出している場合
        if (corners[0].x < screenRect.x + _padding)
        {
            shiftX = (screenRect.x + _padding) - corners[0].x;
        }
        // 右にはみ出している場合
        else if (corners[2].x > screenRect.xMax - _padding)
        {
            shiftX = (screenRect.xMax - _padding) - corners[2].x;
        }

        // --- 垂直方向 (Vertical) ---

        // 下にはみ出している場合
        if (corners[0].y < screenRect.y + _padding)
        {
            shiftY = (screenRect.y + _padding) - corners[0].y;
        }
        // 上にはみ出している場合
        else if (corners[2].y > screenRect.yMax - _padding)
        {
            shiftY = (screenRect.yMax - _padding) - corners[2].y;
        }

        // 補正値を適用
        transform.position += new Vector3(shiftX, shiftY, 0);
    }

    public void Hide()
    {
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }
}
