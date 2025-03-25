namespace App.Reversi
{
    public class ReverseCountDown
    {
        public Position Pos { get; private set; }
        public int Delay { get; set; }

        public ReverseCountDown(Position pos, int delay)
        {
            Pos = pos;
            Delay = delay;
        }
    }
}