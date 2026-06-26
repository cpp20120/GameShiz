namespace Games.Pick.Domain.Configuration;

/// <summary>
/// Tunables for <c>/picklottery</c> + <c>/pickjoin</c>. A lottery is a fixed-
/// stake pool in one chat that fills for a time window then draws a single
/// random entrant. Rake goes to the house; below quorum, all stakes refund.
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
