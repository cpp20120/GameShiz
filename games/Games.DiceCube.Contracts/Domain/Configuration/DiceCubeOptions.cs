namespace Games.DiceCube.Domain.Configuration;

public sealed class DiceCubeOptions
{
    public const string SectionName = "Games:dicecube";
    public int MaxBet { get; init; } = 10_000;

    /// <summary>Used when user sends <c>/dice</c> or <c>/dice bet</c> without an amount.</summary>
    public int DefaultBet { get; init; } = 10;

    public int Mult4 { get; init; } = 1;
    public int Mult5 { get; init; } = 2;
    public int Mult6 { get; init; } = 2;

    /// <summary>0 = disabled. Min seconds after the previous round ended in this chat before a new <c>/dice bet</c>.</summary>
    public int MinSecondsBetweenBets { get; init; } = 8;

    /// <summary>
    /// TTL for the distributed cooldown marker. It is raised automatically to
    /// cover <see cref="MinSecondsBetweenBets"/> so a short configured TTL cannot
    /// weaken the cooldown in a multi-node deployment.
    /// </summary>
    public int CooldownCacheTtlSeconds { get; init; } = 21_600;

    /// <summary>Chance per resolved 🎲 roll to drop a redeem code for one extra 🎲 roll. 0.01 = 1%.</summary>
    public double RedeemDropChance { get; init; }
}
