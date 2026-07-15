namespace CoinFlip.Domain;

public enum CoinSide
{
    Heads,
    Tails,
}

public sealed record CoinFlipGameState(int Flips, int Heads, int Tails)
{
    public static CoinFlipGameState Empty { get; } = new(0, 0, 0);

    public CoinFlipGameState Apply(CoinSide side) => side switch
    {
        CoinSide.Heads => this with { Flips = Flips + 1, Heads = Heads + 1 },
        CoinSide.Tails => this with { Flips = Flips + 1, Tails = Tails + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };
}

public sealed record CoinFlipResult(CoinSide Side, CoinFlipGameState State);

public static class CoinFlipRules
{
    public static CoinFlipResult Flip(CoinFlipGameState state, int entropy)
    {
        ArgumentNullException.ThrowIfNull(state);
        var side = (entropy & 1) == 0 ? CoinSide.Heads : CoinSide.Tails;
        return new CoinFlipResult(side, state.Apply(side));
    }
}
