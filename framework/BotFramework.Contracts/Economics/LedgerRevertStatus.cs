namespace BotFramework.Host.Contracts.Economics;

public enum LedgerRevertStatus
{
    Ok,
    NotFound,
    AlreadyReverted,
    UserMissing,
    NoEffect,
    CorrectionOutOfRange,
}
