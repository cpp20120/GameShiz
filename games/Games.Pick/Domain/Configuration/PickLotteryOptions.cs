namespace Games.Pick;

/// <summary>
/// Tunables for the <c>/pick</c> casino game.
///
/// Game shape: user supplies 2..N variants and (optionally) which one(s) they
/// back. Bot rolls one variant uniformly at random. House takes
/// <see cref="HouseEdge"/> off the gross win; remainder goes to the player.
/// Streak bonus rewards consecutive wins; chain (double-or-nothing) lets the
/// player re-stake their entire payout for another roll.
/// </summary>
public sealed class PickLotteryOptions
{
    /// <summary>How long a fresh pool stays open before the sweeper draws or refunds.</summary>
    public int DurationSeconds { get; init; } = 300;

    /// <summary>Below this entrant count at deadline → refund all and cancel.</summary>
    public int MinEntrantsToSettle { get; init; } = 2;

    /// <summary>Fraction of the pot the house keeps. 0.05 = 5%.</summary>
    public double HouseFeePercent { get; init; } = 0.05;

    /// <summary>Hard cap on a single entrant's stake. 0 = no cap.</summary>
    public int MaxStake { get; init; } = 10_000;

    /// <summary>Lower cap on a single entrant's stake.</summary>
    public int MinStake { get; init; } = 1;

    /// <summary>How often the sweeper polls expired pools.</summary>
    public int SweeperIntervalSeconds { get; init; } = 15;
}
