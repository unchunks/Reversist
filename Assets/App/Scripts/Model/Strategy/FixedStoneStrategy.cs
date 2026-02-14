public class FixedStoneStrategy : StoneStrategy
{
    // Fixedは配置された後の反転ロジック(CanFlip)に関わるが、
    // ここでは配置時のエフェクトはない。
    // TODO: BoardState.SetCellでIsFixedフラグを立てる処理はCore側で行うか、ここで行うか議論の余地あり。
}
