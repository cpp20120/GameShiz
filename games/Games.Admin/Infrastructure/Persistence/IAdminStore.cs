using Dapper;

namespace Games.Admin.Infrastructure.Persistence;

public interface IAdminStore
{
    Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken ct);
    Task<UserSummary?> FindUserAsync(long userId, long balanceScopeId, CancellationToken ct);

    Task<IReadOnlyList<PendingChatBet>> DeletePendingMiniGameBetsAsync(long chatId, CancellationToken ct);

    Task<string?> GetOverrideAsync(string originalName, CancellationToken ct);
    Task UpsertOverrideAsync(string originalName, string newName, CancellationToken ct);
    Task<bool> DeleteOverrideAsync(string originalName, CancellationToken ct);
}
