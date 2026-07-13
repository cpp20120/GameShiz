using BotFramework.Sdk.Execution;
using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class PostgresAtomicQuotaStore : IAtomicQuotaStore
{
    public async Task<QuotaSnapshot> LoadAsync(
        QuotaIdentity quota,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(quota);
        ArgumentNullException.ThrowIfNull(session);
        if (quota.Limit <= 0) return new QuotaSnapshot(0, 0);

        await EnsureRowAsync(quota, session, ct).ConfigureAwait(false);
        var row = await LoadRowAsync(quota, session, ct).ConfigureAwait(false);
        var used = row.RollsOn == quota.OnDate ? row.RollCount : 0;
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

        await EnsureRowAsync(quota, session, ct).ConfigureAwait(false);
        var row = await LoadRowAsync(quota, session, ct).ConfigureAwait(false);
        long used = row.RollsOn == quota.OnDate ? row.RollCount : 0;

        foreach (var effect in effects)
        {
            if (!string.Equals(effect.QuotaId, quota.QuotaId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Quota effect '{effect.QuotaId}' does not target '{quota.QuotaId}'.");
            if (effect.Amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(effects), effect.Amount, "Quota effect amount must be positive.");

            used = effect.Kind switch
            {
                QuotaEffectKind.Consume => checked(used + effect.Amount),
                QuotaEffectKind.Restore => Math.Max(0, used - effect.Amount),
                QuotaEffectKind.Grant => checked(used - effect.Amount),
                _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown quota effect kind."),
            };
        }

        if (used > quota.Limit)
            throw new InvalidOperationException($"Quota '{quota.QuotaId}' would exceed its limit.");
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
                userId = quota.UserId,
                balanceScopeId = quota.BalanceScopeId,
                gameId = quota.GameId,
                onDate = quota.OnDate.ToDateTime(TimeOnly.MinValue),
                used = checked((int)used),
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

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
            cancellationToken: ct)).ConfigureAwait(false);
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
            cancellationToken: ct)).ConfigureAwait(false);
    }

    private sealed record QuotaRow(DateOnly? RollsOn, int RollCount);
}
