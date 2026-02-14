using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.VectorGraphics;
using TMPro;

public class StoneSelectorButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private SVGImage _iconImage;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private TextMeshProUGUI _countText;

    [Header("Settings")]
    [SerializeField] private StoneType _myType;
    [SerializeField] private Color _activeColor = new Color(0.8f, 0.2f, 0.8f, 0.5f);
    [SerializeField] private Color _inactiveColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color _lockedColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

    private StoneSelectorUI _parentUI;
    private int _currentCount;
    private bool _isInfinite;
    private bool _isSelected;
    private bool _isSystemInteractable = true;

    public void Initialize(StoneSelectorUI parent, StoneType type, int count, Sprite iconSprite)
    {
        _parentUI = parent;
        _myType = type;

        if (iconSprite != null)
        {
            _iconImage.sprite = iconSprite;
            _iconImage.preserveAspect = true;
        }

        UpdateCount(count);
        _isSelected = false;
        UpdateVisuals();
    }

    public void UpdateCount(int count)
    {
        _currentCount = count;
        _isInfinite = (count == -1);
        if (_isInfinite) _countText.text = "∞";
        else _countText.text = count.ToString();
        UpdateVisuals();
    }

    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        transform.localScale = isSelected ? Vector3.one * 1.1f : Vector3.one;
        UpdateVisuals();
    }

    public void SetInteractable(bool interactable)
    {
        _isSystemInteractable = interactable;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        bool hasStock = _isInfinite || _currentCount > 0;

        if (!_isSystemInteractable)
        {
            if (_isSelected)
            {
                _backgroundImage.color = _activeColor * 0.5f;
                _iconImage.color = GetColorForType(_myType) * 0.7f;
            }
            else
            {
                _backgroundImage.color = _lockedColor;
                _iconImage.color = GetColorForType(_myType) * 0.3f;
            }
        }
        else if (!hasStock)
        {
            _backgroundImage.color = Color.black;
            _iconImage.color = Color.gray * 0.3f;
        }
        else
        {
            _backgroundImage.color = _isSelected ? _activeColor : _inactiveColor;
            _iconImage.color = GetColorForType(_myType);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isSystemInteractable) return;
        if (!_isInfinite && _currentCount <= 0) return;
        _parentUI.OnButtonSelected(_myType);
    }

    // マウスホバー時の処理
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 使い切っていても説明は見たいので、無条件で表示リクエストを送る
        // 自分の位置(transform.position)を渡して、その上に表示させる
        _parentUI.OnButtonHoverEnter(_myType, transform.position);
    }

    // マウスが離れた時の処理
    public void OnPointerExit(PointerEventData eventData)
    {
        _parentUI.OnButtonHoverExit();
    }

    private Color GetColorForType(StoneType type)
    {
        switch (type)
        {
            case StoneType.Expander: return Color.cyan;
            case StoneType.Bomb: return Color.red;
            case StoneType.Phantom: return new Color(0.7f, 0.5f, 1.0f);
            case StoneType.Spy: return Color.green;
            case StoneType.Fixed: return Color.yellow;
            default: return Color.white;
        }
    }
}
