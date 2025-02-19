public enum StoneType   // 説明は黒視点
{
    Normal,             // 通常の石
    Reverse,            // 一度白として置いてから、すぐに黒として置く
    DelayReverse,       // 一度白として置いてから、次に相手が置いた後に黒として置く
    Frozen,             // 黒のままひっくり返らない

    End                 // 番兵
}
