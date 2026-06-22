namespace Games.Darts;

public enum DartsThrowOutcome
{
    NoBet,
    Thrown,
    // Quick-play bet errors (returned by QuickThrowAsync when auto-bet fails)
    BetInvalid,
    BetNotEnoughCoins,
    BetBusyOtherGame,
    BetDailyLimit,
}
