namespace BotFramework.Host.Contracts.Economics;

public sealed record LedgerHealth(long RowsWindow, long CreditsWindow, long DebitsWindow, long NetWindow,
    long IdempotentRows, long NegativeBalanceRows, long ZeroDeltaRows, DateTimeOffset LastLedgerAt, double LastLedgerAgeSeconds);
