using Dapper;

namespace Games.Admin.Infrastructure.Persistence;

public sealed class AdminStore(INpgsqlConnectionFactory connections, IWalletReadService wallets) : IAdminStore
{
    public async Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken ct)
    {
        var rows = await wallets.ListAsync(ct);
        return rows.OrderByDescending(x => x.Coins).Select(Map).ToList();
    }

    public async Task<UserSummary?> FindUserAsync(long userId, long balanceScopeId, CancellationToken ct)
    {
        var row = await wallets.GetAsync(userId, balanceScopeId, ct);
        return row is null ? null : Map(row);
    }

    public async Task<string?> GetOverrideAsync(string originalName, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT new_name FROM display_name_overrides WHERE original_name = @originalName",
            new { originalName }, cancellationToken: ct));
    }

    private static UserSummary Map(WalletAccount account) => new(
        account.UserId, account.BalanceScopeId, account.DisplayName, account.Coins,
        account.UpdatedAt.ToUnixTimeMilliseconds());
}
