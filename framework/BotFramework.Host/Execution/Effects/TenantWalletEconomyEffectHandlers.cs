using BotFramework.Contracts.Tenancy;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed class PostgresTenantWalletGameEffectHandler
    : GameEffectHandler<TenantWalletEconomyEffect>
{
    protected override async Task ApplyBatchAsync(
        IReadOnlyList<TenantWalletEconomyEffect> effects,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        foreach (var effect in effects)
        {
            var delta = ToDelta(effect);
            await EnsureAsync(effect, context, ct).ConfigureAwait(false);
            var balance = await context.QuerySingleOrDefaultAsync<long?>(
                TenantWalletSql.Update,
                TenantWalletSql.Parameters(effect, delta),
                ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Tenant wallet {effect.TenantId}:{effect.ScopeId}:{effect.PlayerId} is missing or rejected the mutation.");

            await context.ExecuteAsync(
                TenantWalletSql.InsertLedger,
                TenantWalletSql.LedgerParameters(effect, delta, balance, context.OperationId),
                ct).ConfigureAwait(false);
        }
    }

    private static Task EnsureAsync(
        TenantWalletEconomyEffect effect,
        IGameExecutionContext context,
        CancellationToken ct) =>
        context.ExecuteAsync(
            TenantWalletSql.Ensure,
            TenantWalletSql.Parameters(effect, 0),
            ct);

    private static long ToDelta(TenantWalletEconomyEffect effect) => effect.Kind switch
    {
        EconomyEffectKind.Credit => effect.Amount,
        EconomyEffectKind.Debit => -effect.Amount,
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "Unknown wallet effect kind."),
    };
}

internal sealed class PostgresTenantWalletAtomicEffectHandler
    : AtomicEffectHandler<TenantWalletEconomyEffect>
{
    protected override async Task ApplyAsync(
        TenantWalletEconomyEffect effect,
        IAtomicEffectContext context,
        CancellationToken ct)
    {
        var delta = effect.Kind switch
        {
            EconomyEffectKind.Credit => effect.Amount,
            EconomyEffectKind.Debit => -effect.Amount,
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "Unknown wallet effect kind."),
        };
        await context.ExecuteAsync(
            TenantWalletSql.Ensure,
            TenantWalletSql.Parameters(effect, 0),
            ct).ConfigureAwait(false);
        var balance = await context.QuerySingleOrDefaultAsync<long?>(
            TenantWalletSql.Update,
            TenantWalletSql.Parameters(effect, delta),
            ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Tenant wallet {effect.TenantId}:{effect.ScopeId}:{effect.PlayerId} is missing or rejected the mutation.");
        await context.ExecuteAsync(
            TenantWalletSql.InsertLedger,
            TenantWalletSql.LedgerParameters(effect, delta, balance, context.OperationId),
            ct).ConfigureAwait(false);
        context.SetOutput("tenantWalletBalance", balance);
    }
}

internal static class TenantWalletSql
{
    public const string Ensure = """
        INSERT INTO tenant_wallets (tenant_key, scope_key, player_id, display_name)
        SELECT t.tenant_key, s.scope_key, @playerId, @playerId
        FROM tenants t
        JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
        WHERE t.tenant_id = @tenantId
        ON CONFLICT (tenant_key, scope_key, player_id) DO NOTHING
        """;

    public const string Update = """
        UPDATE tenant_wallets w
        SET coins = w.coins + @delta,
            version = w.version + 1,
            updated_at = now()
        WHERE w.tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
          AND w.scope_key = (
              SELECT scope_key FROM tenant_scopes
              WHERE tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
                AND scope_id = @scopeId)
          AND w.player_id = @playerId
          AND w.coins + @delta >= 0
        RETURNING w.coins
        """;

    public const string InsertLedger = """
        INSERT INTO tenant_wallet_ledger
            (tenant_key, scope_key, player_id, delta, balance_after, reason, operation_id)
        SELECT t.tenant_key, s.scope_key, @playerId, @delta, @balance, @reason, @operationId
        FROM tenants t
        JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
        WHERE t.tenant_id = @tenantId
        """;

    public static object Parameters(TenantWalletEconomyEffect effect, long delta) => new
    {
        tenantId = effect.TenantId.Value,
        scopeId = effect.ScopeId.Value,
        playerId = effect.PlayerId.Value,
        delta,
    };

    public static object LedgerParameters(
        TenantWalletEconomyEffect effect,
        long delta,
        long balance,
        string? operationId) => new
    {
        tenantId = effect.TenantId.Value,
        scopeId = effect.ScopeId.Value,
        playerId = effect.PlayerId.Value,
        delta,
        balance,
        reason = effect.Reason,
        operationId,
    };
}
