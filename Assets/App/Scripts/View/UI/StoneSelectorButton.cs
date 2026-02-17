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
    [SerializeField] private Color _activeColor = new Color(0.8f, 0.2f, 0.8f, 0.5f);
    [SerializeField] private Color _inactiveColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color _lockedColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

    private StoneSelectorUI _parentUI;
    private StoneData _myData; // ScriptableObjectを保持
    private RectTransform _rectTransform;

    private int _currentCount;
    private bool _isSelected;
    private bool _isSystemInteractable = true;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(StoneSelectorUI parent, StoneData data, int count)
    {
        _parentUI = parent;
        _myData = data;

        if (_myData != null && _myData.Icon != null)
        {
            _iconImage.sprite = _myData.Icon;
            _iconImage.preserveAspect = true;
        }

        UpdateCount(count);
        _isSelected = false;
        UpdateVisuals();
    }

    public void UpdateCount(int count)
    {
        _currentCount = count;
        _countText.text = (count == -1) ? "∞" : count.ToString();
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
        if (_myData == null) return;
        bool hasStock = (_currentCount == -1 || _currentCount > 0);

        if (!_isSystemInteractable)
        {
            _backgroundImage.color = _isSelected ? _activeColor * 0.5f : _lockedColor;
            _iconImage.color = _myData.ThemeColor * (_isSelected ? 0.7f : 0.3f);
        }
        else if (!hasStock)
        {
            _backgroundImage.color = Color.black;
            _iconImage.color = Color.gray * 0.3f;
        }
        else
        {
            _backgroundImage.color = _isSelected ? _activeColor : _inactiveColor;
            _iconImage.color = _myData.ThemeColor; // データから色を取得
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isSystemInteractable) return;
        if (_currentCount != -1 && _currentCount <= 0) return;
        _parentUI.OnButtonSelected(_myData.Type);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _parentUI.OnButtonHoverEnter(_myData.Type, _rectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _parentUI.OnButtonHoverExit();
    }
}
