using System.Collections.Generic;

/// <summary>
/// 全ての石の基底クラス
/// </summary>
public abstract class StoneStrategy
{
    /// <summary>
    /// 置いた時の反転判定時に使用する色を取得する
    /// (例: Spy石は判定時のみ相手の色として振る舞う)
    /// </summary>
    public virtual StoneColor GetAttackColor(StoneColor myColor) => myColor;

    /// <summary>
    /// 石が置かれ、反転処理が終わった後に発動する効果。
    /// </summary>
    /// <param name="outResult">特殊効果で何が起こったか知る必要がなければnull</param>
    public virtual void OnAfterPlacement(BoardState board, PlayerMove move, List<Position> flippedStones, MoveResult outResult) { }
}
