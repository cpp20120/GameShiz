using System.Collections.Concurrent;

namespace BotFramework.Sdk.MiniGames;
public interface IMiniGameRollGateStore
{
    Task ExpectBotRollAsync(string gameId, long userId, long chatId, CancellationToken ct);
    Task ClearAsync(string gameId, long userId, long chatId, CancellationToken ct);
    Task<bool> ShouldIgnoreUserThrowAsync(string gameId, long userId, long chatId, CancellationToken ct);
}
