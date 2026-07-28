namespace Games.Meta.Application.Clans;

public sealed partial class ClanProjection(IClanService clans, ILogger<ClanProjection> logger)
    : DomainEventSubscriber<GameCompletedMetaEvent>
{
    protected override async Task HandleAsync(GameCompletedMetaEvent ev, CancellationToken ct)
    {
        await clans.ApplyGameCompletedAsync(ev, ct);
        LogClanXpApplied(ev.UserId, ev.ChatId, ev.GameKey);
    }

    [LoggerMessage(EventId = 2701, Level = LogLevel.Debug, Message = "Applied clan XP projection for user {UserId} in chat {ChatId}, game {GameKey}")]
    private partial void LogClanXpApplied(long userId, long chatId, string gameKey);
}
