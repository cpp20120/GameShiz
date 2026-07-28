namespace BotFramework.Host.Contracts.Economics;

public sealed record LedgerReasonVolume(string Reason, long Rows, long Credits, long Debits, long Net);
