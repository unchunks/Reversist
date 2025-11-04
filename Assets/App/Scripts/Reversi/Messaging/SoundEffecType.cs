namespace App.Reversi.Messaging
{
    /// <summary>
    /// 再生するサウンドエフェクトの種類
    /// </summary>
    public enum SoundEffectType
    {
        PutStone,
        Flip,       // 石をひっくり返す音
        FrozenFlip, // 凍結石をひっくり返す音
        Extend,
        Frozen,
        //Broken,
        //Collapse,
        Reverse,    // 特殊効果で石をひっくり返す音
        DelayReverse,
        GameOver
    }
}
