using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using MessagePipe;
using VContainer;

namespace App.Reversi
{
    public class StoneTypePanel : MonoBehaviour
    {
        [SerializeField] private Toggle[] _toggleComponents;

        [Inject] private IPublisher<SelectedStoneTypeInfo> _selectedStoneTypeInfoPublisher;

        private Dictionary<StoneType, TextMeshProUGUI> _labelText { get; set; } = new Dictionary<StoneType, TextMeshProUGUI>();
        private Dictionary<StoneType, TextMeshProUGUI> _countText { get; set; } = new Dictionary<StoneType, TextMeshProUGUI>();

        private StoneColor _observeColor;
        private StoneType _stoneType;
        public StoneType SelectedType
        {
            get => _stoneType;
            private set
            {
                _stoneType = value;
                if (_selectedStoneTypeInfoPublisher == null)
                {
                    Debug.LogError("_stoneTypeInfoPublisherがnullです");
                }
                _selectedStoneTypeInfoPublisher.Publish(new SelectedStoneTypeInfo(_observeColor, _stoneType));
            }
        }

        private void Awake()
        {
            SelectedType = StoneType.Normal;
            foreach (StoneType stoneType in Enum.GetValues(typeof(StoneType)))
            {
                if (stoneType == StoneType.None) continue;

                // LabelとCountのテキストを取得
                TextMeshProUGUI[] targetTexts = _toggleComponents[(int)stoneType].GetComponentsInChildren<TextMeshProUGUI>();
                foreach (TextMeshProUGUI targetText in targetTexts)
                {
                    if (stoneType == StoneType.None)
                    {
                        continue;
                    }

                    if (targetText.name.Equals("Label"))
                    {
                        _labelText[stoneType] = targetText;
                        _labelText[stoneType].text = stoneType.ToString();
                    }
                    else if (targetText.name == "Count")
                    {
                        _countText[stoneType] = targetText;
                    }
                    else
                    {
                        Debug.LogWarning($"正しくないテキスト名が含まれています: {targetText.name}");
                    }
                }
            }
        }

        public void UpdateAvailableCount(StoneType selectStoneType, int availableCount)
        {
            _countText[selectStoneType].text = availableCount.ToString();
        }

        public void OnToggleChanged()
        {
            for (int i = 0; i < _toggleComponents.Length; i++)
            {
                if (_toggleComponents[i].isOn)
                {
                    SelectedType = (StoneType)i;
                }
            }
        }

        public void SetObserveColor(StoneColor color)
        {
            _observeColor = color;
        }
    }
}