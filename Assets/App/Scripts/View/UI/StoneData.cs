using UnityEngine;

[CreateAssetMenu(fileName = "NewStoneData", menuName = "Reversi/Stone Data")]
public class StoneData : ScriptableObject
{
    public StoneType Type;
    public Sprite Icon;
    public Color ThemeColor = Color.white;
    public string Title;
    [TextArea(2, 5)] public string Description;
}
