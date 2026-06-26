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
        return 0;
    }

    public override string ToString() => $"{Category}[{string.Join(',', Tiebreakers)}]";
    public bool Equals(HandRank other) => CompareTo(other) == 0;
}
