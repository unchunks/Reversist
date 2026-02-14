using UnityEngine;
using System.Collections.Generic;

public class StoneSelectorUI : MonoBehaviour
{
    [SerializeField] private StoneSelectorButton _buttonPrefab;
    [SerializeField] private Transform _buttonContainer;

    [SerializeField] private StoneTooltip _tooltip;

    // アイコンの定義リスト
    // Inspectorで「石の種類」と「画像」を紐付ける
    [System.Serializable]
    public struct StoneIconDefinition
    {
        public StoneType Type;
        public Sprite Icon;
        // 説明文データ
        public string Title;
        [TextArea(2, 5)] public string Description;
    }
    [SerializeField] private List<StoneIconDefinition> _iconDefinitions;

    // データ検索用の辞書
    private Dictionary<StoneType, StoneIconDefinition> _definitionMap = new Dictionary<StoneType, StoneIconDefinition>();
    private Dictionary<StoneType, StoneSelectorButton> _buttons = new Dictionary<StoneType, StoneSelectorButton>();

    // 現在UIが参照しているインベントリ実体
    private StoneInventory _currentInventory;
    public StoneType CurrentSelection => _currentInventory != null ? _currentInventory.LastSelected : StoneType.Normal;

    public void Initialize(StoneInventory initialInventory)
    {
        // 定義マップ作成
        _definitionMap.Clear();
        foreach (var def in _iconDefinitions)
        {
            if (!_definitionMap.ContainsKey(def.Type)) _definitionMap.Add(def.Type, def);
        }

        // ボタン生成 (初回のみ)
        // ※黒白で持っている石の種類が違う場合は作り直す必要があるが、
        // 今回は初期種類は同じ前提でボタンを再利用する
        foreach (Transform child in _buttonContainer) Destroy(child.gameObject);
        _buttons.Clear();

        foreach (var kvp in initialInventory.Stock)
        {
            var btn = Instantiate(_buttonPrefab, _buttonContainer);
            Sprite iconSprite = _definitionMap.ContainsKey(kvp.Key) ? _definitionMap[kvp.Key].Icon : null;
            btn.Initialize(this, kvp.Key, kvp.Value, iconSprite);
            _buttons.Add(kvp.Key, btn);
        }

        // 初期インベントリの適用
        SwitchInventory(initialInventory);

        if (_tooltip != null) _tooltip.Hide();
    }

    /// <summary>
    /// 表示するインベントリを切り替える
    /// ターン交代時に呼ばれる
    /// </summary>
    public void SwitchInventory(StoneInventory inventory)
    {
        _currentInventory = inventory;

        // 1. 各ボタンの所持数表示を更新
        foreach (var kvp in _currentInventory.Stock)
        {
            if (_buttons.ContainsKey(kvp.Key))
            {
                _buttons[kvp.Key].UpdateCount(kvp.Value);
            }
        }

        // 2. このインベントリが記憶している「前回の選択」を復元
        // もし前回の選択肢が在庫切れになっていたらNormalに戻す処理を入れる
        if (!_currentInventory.CanUse(_currentInventory.LastSelected))
        {
            _currentInventory.LastSelected = StoneType.Normal;
        }

        RefreshSelectionVisuals();
    }

    public void SetInteractable(bool isInteractable)
    {
        foreach (var btn in _buttons.Values) btn.SetInteractable(isInteractable);
    }

    public void OnButtonSelected(StoneType type)
    {
        if (_currentInventory == null) return;

        // インベントリ側に記憶させる
        _currentInventory.LastSelected = type;

        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        StoneType target = _currentInventory.LastSelected;
        foreach (var kvp in _buttons)
        {
            kvp.Value.SetSelected(kvp.Key == target);
        }
    }

    /// <summary>
    /// 現在選択されている石を1つ消費する
    /// </summary>
    public void ConsumeCurrentSelection()
    {
        if (_currentInventory == null) return;

        StoneType current = _currentInventory.LastSelected;
        _currentInventory.Use(current);

        if (_buttons.ContainsKey(current))
        {
            int newCount = _currentInventory.Stock[current];
            _buttons[current].UpdateCount(newCount);

            // 使い切ったらNormalへ
            if (newCount == 0)
            {
                OnButtonSelected(StoneType.Normal);
            }
        }
    }

    // ツールチップ表示リクエスト処理
    public void OnButtonHoverEnter(StoneType type, Vector3 position)
    {
        if (_tooltip == null) return;
        if (!_definitionMap.ContainsKey(type)) return;

        var def = _definitionMap[type];

        // 石の種類に応じたテーマカラーを取得 (StoneSelectorButtonのロジックと合わせるのが理想だが簡易的に定義)
        Color themeColor = GetThemeColor(type);

        _tooltip.Show(def.Title, def.Description, position, themeColor);
    }

    public void OnButtonHoverExit()
    {
        if (_tooltip != null) _tooltip.Hide();
    }

    private Color GetThemeColor(StoneType type)
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
