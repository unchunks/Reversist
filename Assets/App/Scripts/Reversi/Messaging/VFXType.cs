namespace App.Reversi.Messaging
{
    /// <summary>
    /// 再生するVFX（パーティクルエフェクト）の種類
    /// </summary>
    public enum VFXType
    {
        PutStone,
        Flip,       // 石をひっくり返す
        FrozenFlip, // 凍結石をひっくり返す
        Extend,
        Frozen,
        //Broken,
        //Collapse,
        Reverse,    // 特殊効果で石をひっくり返す
        DelayReverse
    }
}
