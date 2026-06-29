namespace Games.Poker.Domain.Models;

public readonly struct HandRank(HandCategory category, int[] tiebreakers) : IComparable<HandRank>, IEquatable<HandRank>
{
    public HandCategory Category { get; } = category;
    private int[] Tiebreakers { get; } = tiebreakers;

    public int CompareTo(HandRank other)
    {
        var c = ((int)Category).CompareTo((int)other.Category);
        if (c != 0) return c;
        var len = Math.Min(Tiebreakers.Length, other.Tiebreakers.Length);
        for (var i = 0; i < len; i++)
        {
            c = Tiebreakers[i].CompareTo(other.Tiebreakers[i]);
            if (c != 0) return c;
        }
        return Tiebreakers.Length.CompareTo(other.Tiebreakers.Length);
    }

    public override string ToString() => $"{Category}[{string.Join(',', Tiebreakers)}]";
    public bool Equals(HandRank other) =>
        Category == other.Category && Tiebreakers.AsSpan().SequenceEqual(other.Tiebreakers);

    public override bool Equals(object? obj) => obj is HandRank other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Category);
        foreach (var tiebreaker in Tiebreakers)
        {
            hash.Add(tiebreaker);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(HandRank left, HandRank right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HandRank left, HandRank right)
    {
        return !(left == right);
    }

    public static bool operator <(HandRank left, HandRank right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(HandRank left, HandRank right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(HandRank left, HandRank right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(HandRank left, HandRank right)
    {
        return left.CompareTo(right) >= 0;
    }
}
