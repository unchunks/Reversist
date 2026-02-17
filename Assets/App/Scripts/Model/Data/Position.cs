using System;

public struct Position : IEquatable<Position>
{
    public int x;
    public int y;

    public Position(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool Equals(Position other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object obj)
    {
        return obj is Position other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (y * BoardState.MAX_SIZE) + x;
    }

    public static bool operator ==(Position left, Position right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Position left, Position right)
    {
        return !left.Equals(right);
    }
}
