using BotFramework.Contracts.Messaging;

namespace BotFramework.Sdk.Events;

public static class MiniGameRedeemDrops
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
            new MiniGameRedeemCodeDropRequested(
                userId, chatId, gameId, occurredAt, BotChannelContext.Current),
            ct);
    }

    private static bool ShouldDrop(double chance) => chance switch
    {
        <= 0 => false,
        >= 1 => true,
        _ => Random.Shared.NextDouble() < chance,
    };
}
