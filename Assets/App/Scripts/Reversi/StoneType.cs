namespace App.Reversi
{
    public enum StoneType   // 説明は黒視点
    {
        Normal,             // 通常の石
        Extend,             // 盤を1周拡張する
        Frozen,             // 黒のままひっくり返らない
        //Broken,             // 置いた後消える
        //Collapse,           // 周囲1マスの石をすべて消す
        Reverse,            // 一度白として置いてから、すぐに黒として置く
        DelayReverse,       // 一度白として置いてから、次に相手が置いた後に黒として置く
        None
    }

    public static class StoneTypeExtentions
    {
        public static bool IsReverseType(this StoneType type)
        {
            switch (type)
            {
                case StoneType.Reverse:
                case StoneType.DelayReverse:
                    return true;
                default:
                    return false;
            }
        }

        public static string ToString(this StoneType type)
        {
            switch (type)
            {
                case StoneType.Normal:          return "ノーマル";
                case StoneType.Frozen:          return "固定";
                case StoneType.Reverse:         return "反転";
                case StoneType.DelayReverse:    return "遅延反転";
                default:                        return "なし";
            }
        }
    }
}