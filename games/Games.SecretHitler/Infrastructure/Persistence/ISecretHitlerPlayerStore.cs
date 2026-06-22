using BotFramework.Host;
using Dapper;

namespace Games.SecretHitler.Infrastructure.Persistence;

public interface ISecretHitlerPlayerStore
{
    Task<SecretHitlerPlayer?> FindByUserAsync(long userId, CancellationToken ct);
    Task<List<SecretHitlerPlayer>> ListByGameAsync(string inviteCode, CancellationToken ct);
    Task<bool> AnyForUserAsync(long userId, CancellationToken ct);
    Task<int> CountByGameAsync(string inviteCode, CancellationToken ct);
    Task InsertAsync(SecretHitlerPlayer player, CancellationToken ct);
    Task UpdateAsync(SecretHitlerPlayer player, CancellationToken ct);
    Task DeleteAsync(string inviteCode, int position, CancellationToken ct);
    Task UpsertStateMessageAsync(long userId, int messageId, CancellationToken ct);
}
