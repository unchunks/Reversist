using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StoneSelectPanel : MonoBehaviour
{
    [SerializeField] public Toggle[] ToggleComponents;

    public Dictionary<StoneType, TextMeshProUGUI> Label { get; set; } = new Dictionary<StoneType, TextMeshProUGUI>();
    public Dictionary<StoneType, TextMeshProUGUI> Count { get; set; } = new Dictionary<StoneType, TextMeshProUGUI>();
    public AvailableStoneCount AvailableStoneCount { get; private set; } = new AvailableStoneCount();

    private void Awake()
    {
        foreach (StoneType stoneType in Enum.GetValues(typeof(StoneType)))
        {
            // LabelとCountのテキストを取得
            TextMeshProUGUI[] targetTexts = ToggleComponents[(int)stoneType].GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI targetText in targetTexts)
            {
                if (targetText.name.Equals("Label"))
                {
                    Label[stoneType] = targetText;
                    Label[stoneType].text = stoneType.ToString();
                }
                else if (targetText.name == "Count")
                {
                    Count[stoneType] = targetText;
                }
                else
                {
                    Debug.LogWarning($"正しくないテキスト名が含まれています: {targetText.name}");
                }
            }
        }

        AvailableStoneCount.Count.OnValueChanged += (selectStoneType, availableCount) =>
        {
            UpdateCount(selectStoneType, availableCount);
        };

        foreach (StoneType stoneType in Enum.GetValues(typeof(StoneType)))
        {
            if (AvailableStoneCount.Count.ContainsKey(stoneType))
            {
                UpdateCount(stoneType, AvailableStoneCount.Count[stoneType]);
            }
            else
            {
                Debug.LogWarning($"StoneType {stoneType} is not present in AvailableStoneCount.Count");
            }
        }
    }

    private void Start()
    {
    }

    public void UpdateCount(StoneType selectStoneType, int availableCount)
    {
        Debug.Log("UpdateCount");
        if (Count.ContainsKey(selectStoneType))
        {
            Count[selectStoneType].text = availableCount.ToString();
        }
        else
        {
            Debug.LogWarning($"Count dictionary does not contain key: {selectStoneType}");
        }
    }
}
