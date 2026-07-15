namespace BotFramework.Host.Contracts.Discord;

public interface IDiscordOutbox
{
    Task EnqueueAsync(DiscordOutboxMessage message, CancellationToken ct);
}
