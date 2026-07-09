namespace BotFramework.Sdk.Events.Telegram;
public static class TelegramMiniGameRedeemDrops
{
    public static Task MaybePublishAsync(
        IDomainEventBus events,
        double chance,
        long userId,
        long chatId,
        string gameId,
        long occurredAt,
        CancellationToken ct)
    {
        if (!ShouldDrop(chance))
            return Task.CompletedTask;

        return events.PublishAsync(
            new TelegramMiniGameRedeemCodeDropRequested(userId, chatId, gameId, occurredAt),
            ct);
    }

    private static bool ShouldDrop(double chance)
    {
        return chance switch
        {
            <= 0 => false,
            >= 1 => true,
            _ => Random.Shared.NextDouble() < chance,
        };
    }
}
