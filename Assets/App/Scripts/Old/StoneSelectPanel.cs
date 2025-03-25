using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StoneSelectPanel : MonoBehaviour
{
    [SerializeField] public Toggle[] ToggleComponents;

    public Dictionary<StoneType, TextMeshProUGUI> LabelText { get; set; } = new Dictionary<StoneType, TextMeshProUGUI>();
    public Dictionary<StoneType, TextMeshProUGUI> CountText { get; set; } = new Dictionary<StoneType, TextMeshProUGUI>();
    public AvailableStoneCount AvailableStoneCount { get; private set; } = new AvailableStoneCount();

    public void Awake()
    {
        foreach (StoneType stoneType in Enum.GetValues(typeof(StoneType)))
        {
            // LabelとCountのテキストを取得
            TextMeshProUGUI[] targetTexts = ToggleComponents[(int)stoneType].GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI targetText in targetTexts)
            {
                if (targetText.name.Equals("Label"))
                {
                    LabelText[stoneType] = targetText;
                    LabelText[stoneType].text = stoneType.ToString();
                }
                else if (targetText.name == "Count")
                {
                    CountText[stoneType] = targetText;
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
            UpdateCount(stoneType, AvailableStoneCount.Count[stoneType]);
        }
    }

    public void UpdateCount(StoneType selectStoneType, int availableCount)
    {
        CountText[selectStoneType].text = availableCount.ToString();
    }
}
