namespace Games.Pick.Domain.Configuration;

/// <summary>
/// Tunables for the <c>/pick</c> casino game.
///
/// Game shape: user supplies 2..N variants and (optionally) which one(s) they
/// back. Bot rolls one variant uniformly at random. House takes
/// <see cref="HouseEdge"/> off the gross win; remainder goes to the player.
/// Streak bonus rewards consecutive wins; chain (double-or-nothing) lets the
/// player re-stake their entire payout for another roll.
/// </summary>
public sealed class PickOptions
{
    public const string SectionName = "Games:pick";

    // ── core economy ──────────────────────────────────────────────────────────

    /// <summary>Default stake when the user types <c>/pick a, b</c> with no number.</summary>
    public int DefaultBet { get; init; } = 10;

    /// <summary>Hard upper bound on a single bet; 0 = no cap.</summary>
    public int MaxBet { get; init; } = 5_000;

    /// <summary>Minimum number of variants the user must supply.</summary>
    public int MinVariants { get; init; } = 2;

    /// <summary>Maximum number of variants accepted in one /pick call.</summary>
    public int MaxVariants { get; init; } = 10;

    /// <summary>Per-variant length cap (chars). Trims excess instead of rejecting.</summary>
    public int MaxVariantLength { get; init; } = 64;

    /// <summary>
    /// Fraction of gross win the house keeps. 0.05 = 5% rake. With N variants
    /// backed k of them, gross = bet × (N / k); player receives floor(gross × (1 − HouseEdge)).
    /// </summary>
    public double HouseEdge { get; init; } = 0.05;

    // ── streak multiplier ────────────────────────────────────────────────────

    /// <summary>Per-win bonus rate. 0.5 means each consecutive win after the first adds 50% of the stake.</summary>
    public double StreakBonusPerWin { get; init; } = 0.5;

    /// <summary>Hard cap on the streak factor used for the bonus. 4 = bonus tops out at the 5th consecutive win.</summary>
    public int StreakCap { get; init; } = 4;

    // ── double-or-nothing chain ───────────────────────────────────────────────

    /// <summary>Maximum chain depth. 0 disables the chain button entirely; 5 means at most 5 successive doubles.</summary>
    public int ChainMaxDepth { get; init; } = 5;

    /// <summary>How long a chain offer button stays valid (seconds). After that the inline button is dead and ignored.</summary>
    public int ChainTtlSeconds { get; init; } = 60;

    // ── suspense reveal ───────────────────────────────────────────────────────

    /// <summary>Animate the result by editing the message and eliminating losing variants one by one.</summary>
    public bool RevealAnimation { get; init; } = true;

    /// <summary>Delay between each elimination step in the reveal animation.</summary>
    public int RevealStepDelayMs { get; init; } = 600;

    /// <summary>Maximum total time the reveal animation may take. Caps spam at large N.</summary>
    public int RevealMaxTotalMs { get; init; } = 4_500;

    // ── lottery (multi-user pool) ─────────────────────────────────────────────

    public PickLotteryOptions Lottery { get; init; } = new();

    // ── daily lottery (per-chat, draws at local midnight) ────────────────────

    public PickDailyLotteryOptions Daily { get; init; } = new();
}

/// <summary>
/// Tunables for <c>/picklottery</c> + <c>/pickjoin</c>. A lottery is a fixed-
/// stake pool in one chat that fills for a time window then draws a single
/// random entrant. Rake goes to the house; below quorum, all stakes refund.
/// </summary>

/// <summary>
/// Tunables for the per-chat <c>/dailylottery</c>: every chat gets one daily
/// pool that opens lazily on the first ticket purchase and draws at the next
/// local-midnight (UTC + <see cref="TimezoneOffsetHoursOverride"/> if set,
/// otherwise the global TelegramDiceDailyLimit offset).
/// </summary>
