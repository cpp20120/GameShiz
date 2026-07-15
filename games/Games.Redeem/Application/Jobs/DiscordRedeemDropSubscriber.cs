using BotFramework.Contracts.Messaging;
using BotFramework.Host.Contracts.Discord;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Redeem.Application.Jobs;

public sealed partial class DiscordRedeemDropSubscriber(
    IServiceProvider services,
    IDiscordOutbox discordOutbox,
    ILogger<DiscordRedeemDropSubscriber> logger) : IDomainEventSubscriber
{
    public async Task HandleAsync(IDomainEvent ev, CancellationToken ct)
    {
        if (ev is not MiniGameRedeemCodeDropRequested drop
            || drop.Channel != BotChannel.Discord
            || drop.UserId == 0 || drop.ChatId == 0
            || string.IsNullOrWhiteSpace(drop.GameId)) return;

        try
        {
            await using var scope = services.CreateAsyncScope();
            var redeem = scope.ServiceProvider.GetRequiredService<IRedeemService>();
            var code = await redeem.IssueAdminCodeAsync(userId: 0, ct, drop.GameId);
            await discordOutbox.EnqueueAsync(
                new DiscordOutboxMessage(
                    drop.UserId,
                    drop.ChatId,
                    $"🎟 Выпал фриспин!\nИспользуй `/redeem {code}`",
                    Title: "CasinoShiz · Redeem",
                    DedupeKey: $"redeem-drop:{drop.Channel.ToString().ToLowerInvariant()}:{drop.UserId}:{drop.ChatId}:{drop.GameId}:{drop.OccurredAt}"),
                ct);
        }
        catch (Exception ex)
        {
            LogDropFailed(ex);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Warning, "redeem.discord_drop_failed")]
    partial void LogDropFailed(Exception ex);
}
