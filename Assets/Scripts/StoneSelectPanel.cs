using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StoneSelectPanel : MonoBehaviour
{
    [SerializeField] public Toggle[] ToggleComponents;

    public Dictionary<StoneType, TextMeshProUGUI> Label { get; set; }
    public Dictionary<StoneType, TextMeshProUGUI> Count { get; set; }

    void Start()
    {
        StoneType[] stoneTypes = (StoneType[])Enum.GetValues(typeof(StoneType));

        Label = new Dictionary<StoneType, TextMeshProUGUI>();
        Count = new Dictionary<StoneType, TextMeshProUGUI>();

        for (int i = 0; i < Mathf.Min(ToggleComponents.Length, stoneTypes.Length); i++)
        {
            // LabelとCountのテキストを取得
            TextMeshProUGUI[] targetTexts = ToggleComponents[i].GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI targetText in targetTexts)
            {
                if (targetText.name.Equals("Label"))
                {
                    Label[stoneTypes[i]] = targetText;
                    Label[stoneTypes[i]].text = stoneTypes[i].ToString();
                }
                else if (targetText.name == "Count")
                {
                    Count[stoneTypes[i]] = targetText;
                }
                else
                {
                    Debug.LogWarning($"正しくないテキスト名が含まれています: {targetText.name}");
                }
            }
        }
    }

    public void UpdateAvailableNums(Dictionary<StoneType, int> stoneCount)
    {
        foreach (StoneType stoneType in Count.Keys)
        {
            Count[stoneType].text = stoneCount[stoneType].ToString();
        }
    }
}
