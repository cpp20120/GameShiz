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

    public async Task<IReadOnlyList<PendingChatBet>> DeletePendingMiniGameBetsAsync(long chatId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var deleted = new List<PendingChatBet>();
        deleted.AddRange(await conn.QueryAsync<PendingChatBet>(new CommandDefinition("""
            DELETE FROM dicecube_bets
            WHERE chat_id = @chatId
            RETURNING 'dicecube' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { chatId }, transaction: tx, cancellationToken: ct)));
        deleted.AddRange(await conn.QueryAsync<PendingChatBet>(new CommandDefinition("""
            DELETE FROM football_bets
            WHERE chat_id = @chatId
            RETURNING 'football' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { chatId }, transaction: tx, cancellationToken: ct)));
        deleted.AddRange(await conn.QueryAsync<PendingChatBet>(new CommandDefinition("""
            DELETE FROM basketball_bets
            WHERE chat_id = @chatId
            RETURNING 'basketball' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { chatId }, transaction: tx, cancellationToken: ct)));
        deleted.AddRange(await conn.QueryAsync<PendingChatBet>(new CommandDefinition("""
            DELETE FROM bowling_bets
            WHERE chat_id = @chatId
            RETURNING 'bowling' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { chatId }, transaction: tx, cancellationToken: ct)));
        deleted.AddRange(await conn.QueryAsync<PendingChatBet>(new CommandDefinition("""
            DELETE FROM darts_rounds
            WHERE chat_id = @chatId AND status IN (@queued, @awaiting)
            RETURNING 'darts' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, bot_message_id AS BotMessageId
            """,
            new
            {
                chatId,
                queued = (short)Games.Darts.Domain.Results.DartsRoundStatus.Queued,
                awaiting = (short)Games.Darts.Domain.Results.DartsRoundStatus.AwaitingOutcome,
            },
            transaction: tx, cancellationToken: ct)));

        await tx.CommitAsync(ct);
        return deleted;
    }

    public async Task<string?> GetOverrideAsync(string originalName, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT new_name FROM display_name_overrides WHERE original_name = @originalName",
            new { originalName }, cancellationToken: ct));
    }

    public async Task UpsertOverrideAsync(string originalName, string newName, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO display_name_overrides (original_name, new_name)
            VALUES (@originalName, @newName)
            ON CONFLICT (original_name) DO UPDATE SET new_name = EXCLUDED.new_name
            """;
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { originalName, newName }, cancellationToken: ct));
    }

    public async Task<bool> DeleteOverrideAsync(string originalName, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM display_name_overrides WHERE original_name = @originalName",
            new { originalName }, cancellationToken: ct));
        return rows > 0;
    }

    private static UserSummary Map(WalletAccount account) => new(
        account.UserId, account.BalanceScopeId, account.DisplayName, account.Coins,
        account.UpdatedAt.ToUnixTimeMilliseconds());
}
