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
public sealed class PickDailyLotteryOptions
{
    /// <summary>Coins per ticket. Each user can buy as many as they can afford.</summary>
    public int TicketPrice { get; init; } = 50;

    /// <summary>Hard cap on tickets per user per day. 0 = unlimited.</summary>
    public int MaxTicketsPerUserPerDay { get; init; } = 0;

    /// <summary>Hard cap on tickets bought in one single command. Throttles spam.</summary>
    public int MaxTicketsPerBuyCommand { get; init; } = 100;

    /// <summary>Fraction of pot the house keeps. 0.05 = 5%.</summary>
    public double HouseFeePercent { get; init; } = 0.05;

    /// <summary>How often the sweeper polls expired pools (seconds).</summary>
    public int SweeperIntervalSeconds { get; init; } = 60;

    /// <summary>How many history entries <c>/dailylottery history</c> shows.</summary>
    public int HistoryLimit { get; init; } = 7;

    /// <summary>
    /// If non-zero, override the day-boundary offset. Otherwise we read it
    /// from <c>TelegramDiceDailyLimit:TimezoneOffsetHours</c> at runtime.
    /// </summary>
    public int TimezoneOffsetHoursOverride { get; init; } = 0;

    /// <summary>
    /// Local hour-of-day at which the cycle closes and the sweeper draws.
    /// 0..23. Default 18 (= 18:00 local). Setting 0 keeps the historical
    /// midnight-rollover behaviour. The cycle key (<c>day_local</c>) is the
    /// CALENDAR DATE OF THE DRAW — buying right after a draw lands you in
    /// the next day's pool, no dead window.
    /// </summary>
    public int DrawHourLocal { get; init; } = 18;
}
