namespace Games.Admin.Infrastructure.Persistence;

public interface IAdminStore
{
    Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken ct);
    Task<UserSummary?> FindUserAsync(long userId, long balanceScopeId, CancellationToken ct);

    Task<string?> GetOverrideAsync(string originalName, CancellationToken ct);
}
