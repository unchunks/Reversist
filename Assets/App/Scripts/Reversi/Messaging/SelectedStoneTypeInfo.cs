namespace App.Reversi
{
    /// <summary>
    /// 選択した石の種類が変わったことを伝えるデータクラス
    /// </summary>
    public class SelectedStoneTypeInfo
    {
        public StoneColor Color;
        public StoneType Type;

        public SelectedStoneTypeInfo(StoneColor color, StoneType type)
        {
            Color = color;
            Type = type;
        }
    }
}