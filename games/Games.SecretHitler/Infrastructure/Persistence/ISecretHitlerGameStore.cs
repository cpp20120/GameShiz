using BotFramework.Host;
using Dapper;

namespace Games.SecretHitler.Infrastructure.Persistence;

public interface ISecretHitlerGameStore
{
    Task<SecretHitlerGame?> FindAsync(string inviteCode, CancellationToken ct);
    Task<SecretHitlerGame?> FindOpenByChatAsync(long chatId, CancellationToken ct);
    Task<bool> CodeExistsAsync(string inviteCode, CancellationToken ct);
    Task InsertAsync(SecretHitlerGame game, CancellationToken ct);
    Task UpdateAsync(SecretHitlerGame game, CancellationToken ct);
    Task UpsertStateMessageAsync(string inviteCode, int messageId, CancellationToken ct);
}
