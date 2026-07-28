using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

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
            ct);
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
        context.SetOutput("tenantWalletBalance", balance);
    }
}
