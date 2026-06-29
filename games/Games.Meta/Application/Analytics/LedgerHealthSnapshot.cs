namespace Games.Meta.Application.Analytics;

internal sealed record LedgerHealthSnapshot(
    long RowsWindow,
    long CreditsWindow,
    long DebitsWindow,
    long NetWindow,
    long IdempotentRows,
    long NegativeBalanceRows,
    long ZeroDeltaRows,
    DateTime LastLedgerAt,
    double LastLedgerAgeSeconds);
