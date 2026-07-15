using BotFramework.Host.Contracts.Discord;

namespace BotFramework.Host.DiscordOutbox;

public interface IDiscordOutboxStore : IDiscordOutbox
{
    Task<IReadOnlyList<DiscordOutboxRow>> ClaimDueAsync(int limit, TimeSpan lease, CancellationToken ct);
    Task MarkSentAsync(long id, long discordMessageId, CancellationToken ct);
    Task MarkFailedAsync(long id, string errorMessage, TimeSpan retryAfter, CancellationToken ct);
}
