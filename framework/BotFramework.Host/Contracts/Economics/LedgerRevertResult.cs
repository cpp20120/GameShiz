namespace BotFramework.Host.Contracts.Economics;

public readonly record struct LedgerRevertResult(LedgerRevertStatus Status, int NewBalance = 0);
