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
            await EnsureAsync(effect, context, ct);
            var balance = await context.QuerySingleOrDefaultAsync<long?>(
                TenantWalletSql.Update,
                TenantWalletSql.Parameters(effect, delta),
                ct)
                ?? throw new InvalidOperationException(
                    $"Tenant wallet {effect.TenantId}:{effect.ScopeId}:{effect.PlayerId} is missing or rejected the mutation.");

            await context.ExecuteAsync(
                TenantWalletSql.InsertLedger,
                TenantWalletSql.LedgerParameters(effect, delta, balance, context.OperationId),
                ct);
        }
    }

    private static Task<int> EnsureAsync(
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
