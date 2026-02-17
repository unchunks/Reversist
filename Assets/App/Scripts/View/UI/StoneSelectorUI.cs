using UnityEngine;
using System.Collections.Generic;

public class StoneSelectorUI : MonoBehaviour
{
    [SerializeField] private StoneSelectorButton _buttonPrefab;
    [SerializeField] private Transform _buttonContainer;
    [SerializeField] private StoneTooltip _tooltip;

    // ScriptableObjectのリストをInspectorでセットする
    [SerializeField] private List<StoneData> _stoneDataList;

    private StoneData[] _dataMap;
    private StoneSelectorButton[] _buttons;

    private StoneInventory _currentInventory;
    public StoneType CurrentSelection => _currentInventory?.LastSelected ?? StoneType.Normal;

    public void Initialize(StoneInventory initialInventory)
    {
        _dataMap = new StoneData[(int)StoneType.Size];
        foreach (var data in _stoneDataList)
        {
            _dataMap[(int)data.Type] = data;
        }

        foreach (Transform child in _buttonContainer) Destroy(child.gameObject);

        _buttons = new StoneSelectorButton[(int)StoneType.Size];

        for (int i = 0; i < (int)StoneType.Size; i++)
        {
            var btn = Instantiate(_buttonPrefab, _buttonContainer);
            // StoneData そのものを渡す
            btn.Initialize(this, _dataMap[i], initialInventory.Stock[i]);
            _buttons[i] = btn;
        }

        SwitchInventory(initialInventory);
        if (_tooltip != null) _tooltip.Hide();
    }

    public void SwitchInventory(StoneInventory inventory)
    {
        _currentInventory = inventory;

        for (int i = 0; i < (int)StoneType.Size; i++)
        {
            if (_buttons[i] != null) _buttons[i].UpdateCount(_currentInventory.Stock[i]);
        }

        if (!_currentInventory.CanUse(_currentInventory.LastSelected))
        {
            _currentInventory.LastSelected = StoneType.Normal;
        }

        RefreshSelectionVisuals();
    }

    public void SetInteractable(bool isInteractable)
    {
        foreach (var btn in _buttons) if (btn != null) btn.SetInteractable(isInteractable);
    }

    public void OnButtonSelected(StoneType type)
    {
        if (_currentInventory == null) return;
        _currentInventory.LastSelected = type;
        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        StoneType target = _currentInventory.LastSelected;
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] != null) _buttons[i].SetSelected((StoneType)i == target);
        }
    }

    public void ConsumeCurrentSelection()
    {
        if (_currentInventory == null) return;

        StoneType current = _currentInventory.LastSelected;
        _currentInventory.Use(current);
        int newCount = _currentInventory.Stock[(int)current];

        if (_buttons[(int)current] != null)
        {
            _buttons[(int)current].UpdateCount(newCount);
            if (newCount == 0) OnButtonSelected(StoneType.Normal);
        }
    }

    // ツールチップ呼び出し
    public void OnButtonHoverEnter(StoneType type, RectTransform buttonRect)
    {
        if (_tooltip == null) return;
        StoneData data = _dataMap[(int)type];
        if (data == null) return;

        _tooltip.Show(data, buttonRect);
    }

    public void OnButtonHoverExit()
    {
        if (_tooltip != null) _tooltip.Hide();
    }
}
