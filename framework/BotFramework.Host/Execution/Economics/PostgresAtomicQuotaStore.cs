using BotFramework.Sdk.Execution;
using BotFramework.Sdk.Economics;
using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class PostgresAtomicQuotaStore(WalletScopeResolver? scopeResolver = null) : IAtomicQuotaStore
{
    public async Task<QuotaSnapshot> LoadAsync(
        QuotaIdentity quota,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(quota);
        ArgumentNullException.ThrowIfNull(session);
        if (quota.Limit <= 0) return new QuotaSnapshot(0, 0);

        var effectiveQuota = await ResolveAsync(quota, session, ct);
        await EnsureRowAsync(effectiveQuota, session, ct);
        var row = await LoadRowAsync(effectiveQuota, session, ct);
        long used = row.RollsOn == quota.OnDate ? row.RollCount : 0;
        return new QuotaSnapshot(used, quota.Limit);
    }

    public async Task<QuotaSnapshot> ApplyAsync(
        QuotaIdentity quota,
        IReadOnlyList<QuotaEffect> effects,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(quota);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(session);

        if (quota.Limit <= 0)
        {
            if (effects.Count != 0)
                throw new InvalidOperationException($"Unlimited quota '{quota.QuotaId}' cannot be mutated.");
            return new QuotaSnapshot(0, 0);
        }

        var effectiveQuota = await ResolveAsync(quota, session, ct);
        await EnsureRowAsync(effectiveQuota, session, ct);
        var row = await LoadRowAsync(effectiveQuota, session, ct);
        long used = row.RollsOn == quota.OnDate ? row.RollCount : 0;
        var decision = QuotaPolicy.Apply(new QuotaSnapshot(used, quota.Limit), quota.QuotaId, effects);
        if (decision.Rejected)
            throw new InvalidOperationException($"Quota '{quota.QuotaId}' would exceed its limit.");
        used = decision.NewUsed;
        if (used > int.MaxValue)
            throw new InvalidOperationException($"Quota '{quota.QuotaId}' exceeds the supported storage range.");

        const string sql = """
            UPDATE telegram_dice_daily_rolls
            SET rolls_on = @onDate,
                roll_count = @used,
                updated_at = now()
            WHERE telegram_user_id = @userId
              AND balance_scope_id = @balanceScopeId
              AND game_id = @gameId
            """;
        await session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                userId = effectiveQuota.UserId,
                balanceScopeId = effectiveQuota.BalanceScopeId,
                gameId = effectiveQuota.GameId,
                onDate = effectiveQuota.OnDate.ToDateTime(TimeOnly.MinValue),
                used = checked((int)used),
            },
            session.Transaction,
            cancellationToken: ct));

        return new QuotaSnapshot(used, quota.Limit);
    }

    private static async Task EnsureRowAsync(
        QuotaIdentity quota,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO telegram_dice_daily_rolls (
                telegram_user_id,
                balance_scope_id,
                game_id,
                rolls_on,
                roll_count)
            VALUES (@userId, @balanceScopeId, @gameId, @onDate, 0)
            ON CONFLICT (telegram_user_id, balance_scope_id, game_id) DO NOTHING
            """;
        await session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                userId = quota.UserId,
                balanceScopeId = quota.BalanceScopeId,
                gameId = quota.GameId,
                onDate = quota.OnDate.ToDateTime(TimeOnly.MinValue),
            },
            session.Transaction,
            cancellationToken: ct));
    }

    private static async Task<QuotaRow> LoadRowAsync(
        QuotaIdentity quota,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        const string sql = """
            SELECT rolls_on AS RollsOn,
                   roll_count AS RollCount
            FROM telegram_dice_daily_rolls
            WHERE telegram_user_id = @userId
              AND balance_scope_id = @balanceScopeId
              AND game_id = @gameId
            FOR UPDATE
            """;
        return await session.Connection.QuerySingleAsync<QuotaRow>(new CommandDefinition(
            sql,
            new
            {
                userId = quota.UserId,
                balanceScopeId = quota.BalanceScopeId,
                gameId = quota.GameId,
            },
            session.Transaction,
            cancellationToken: ct));
    }

    private async Task<QuotaIdentity> ResolveAsync(
        QuotaIdentity quota,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        if (scopeResolver is null)
            return quota;

        var effectiveScopeId = await scopeResolver.ResolveAsync(
            quota.BalanceScopeId,
            session.Connection,
            session.Transaction,
            ct);
        return quota with { BalanceScopeId = effectiveScopeId };
    }

    private sealed record QuotaRow(DateOnly? RollsOn, int RollCount);
}
