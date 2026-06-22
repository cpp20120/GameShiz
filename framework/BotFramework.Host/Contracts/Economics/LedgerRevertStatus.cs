namespace BotFramework.Host;

public enum LedgerRevertStatus
{
    Ok,
    NotFound,
    AlreadyReverted,
    UserMissing,
    NoEffect,
    CorrectionOutOfRange,
}
