using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Economics.Services;

public sealed class WalletReadService(
    INpgsqlConnectionFactory connections,
    WalletScopeResolver? scopeResolver = null) : IWalletReadService
{
    private const string Select = """
        SELECT telegram_user_id AS UserId, balance_scope_id AS BalanceScopeId,
               display_name AS DisplayName, coins AS Coins,
               created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM users
        """;

    public async Task<WalletAccount?> GetAsync(long userId, long balanceScopeId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var effectiveScopeId = scopeResolver is null
            ? balanceScopeId
            : await scopeResolver.ResolveAsync(balanceScopeId, connection, null, ct);
        if (scopeResolver is not null)
            await scopeResolver.CopyUserToAliasAsync(
                userId, balanceScopeId, effectiveScopeId, connection, null, ct);
        return await connection.QuerySingleOrDefaultAsync<WalletAccount>(new CommandDefinition(
            Select + " WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId",
            new { userId, balanceScopeId = effectiveScopeId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<WalletAccount>> ListAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return (await connection.QueryAsync<WalletAccount>(new CommandDefinition(Select, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<WalletAccount>> ListByScopeAsync(long balanceScopeId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var effectiveScopeId = scopeResolver is null
            ? balanceScopeId
            : await scopeResolver.ResolveAsync(balanceScopeId, connection, null, ct);
        if (scopeResolver is not null)
            await scopeResolver.CopyScopeToAliasAsync(
                balanceScopeId, effectiveScopeId, connection, null, ct);
        return (await connection.QueryAsync<WalletAccount>(new CommandDefinition(
            Select + " WHERE balance_scope_id = @balanceScopeId", new { balanceScopeId = effectiveScopeId }, cancellationToken: ct))).AsList();
    }

    public async Task<bool> ExistsAsync(long userId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM users WHERE telegram_user_id = @userId)",
            new { userId }, cancellationToken: ct));
    }
}
