namespace BotFramework.Sdk.Execution;

public enum TurnRejectionReason
{
    StaleRevision,
    GameNotActive,
    NotPlayersTurn,
    TurnExpired,
}
