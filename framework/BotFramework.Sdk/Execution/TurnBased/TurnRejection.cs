namespace BotFramework.Sdk.Execution;

public sealed record TurnRejection<TPlayerId>(
    TurnRejectionReason Reason,
    long CurrentRevision,
    TPlayerId CurrentPlayerId,
    DateTimeOffset? TurnDeadline)
{
    public string Code => Reason switch
    {
        TurnRejectionReason.StaleRevision => "stale_revision",
        TurnRejectionReason.GameNotActive => "game_not_active",
        TurnRejectionReason.NotPlayersTurn => "not_players_turn",
        TurnRejectionReason.TurnExpired => "turn_expired",
        _ => throw new InvalidOperationException($"Unknown turn rejection reason '{Reason}'."),
    };
}
