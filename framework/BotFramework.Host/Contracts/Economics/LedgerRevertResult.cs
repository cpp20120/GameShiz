namespace BotFramework.Host;

public readonly record struct LedgerRevertResult(LedgerRevertStatus Status, int NewBalance = 0);
