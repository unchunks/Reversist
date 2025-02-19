public enum State
{
    None,
    Black,
    White
}

public static class StateExtentions
{
    public static State Opponent(this State state)
    {
        if (state == State.Black)
        {
            return State.White;
        }
        if (state == State.White)
        {
            return State.Black;
        }

        return State.None;
    }
}