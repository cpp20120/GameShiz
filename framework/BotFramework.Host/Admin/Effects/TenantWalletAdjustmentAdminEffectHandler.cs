using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Effects;

namespace BotFramework.Host.Admin.Effects;

internal sealed class TenantWalletAdjustmentAdminEffectHandler
    : AdminEffectHandler<TenantWalletAdjustmentAdminEffect>
{
    protected override async Task ApplyAsync(
        TenantWalletAdjustmentAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        await TenantWalletAdminSql.EnsureAsync(
            effect.TenantId,
            effect.ScopeId,
            effect.PlayerId,
            effect.DisplayName,
            context,
            ct);
        if (await TenantWalletAdminSql.TryExistingOperationAsync(effect.TenantId, effect.ScopeId, effect.OperationId, context, ct)
                 is { } existing)
        {
            context.SetOutput("balance", existing);
            return;
        }

        var row = await TenantWalletAdminSql.LockAsync(effect.TenantId, effect.ScopeId, effect.PlayerId, context, ct)
            ?? throw new InvalidOperationException(
                $"Tenant wallet {effect.TenantId}:{effect.ScopeId}:{effect.PlayerId} does not exist.");
        var balance = checked(row.Balance + effect.Delta);
        if (!effect.AllowNegative && balance < 0)
            throw new InvalidOperationException("The tenant wallet cannot become negative.");

        await TenantWalletAdminSql.ApplyAsync(
            effect.TenantId,
            effect.ScopeId,
            effect.PlayerId,
            balance,
            checked(row.Version + 1),
            effect.Delta,
            effect.Reason,
            effect.OperationId,
            context,
            ct);
        context.SetOutput("balance", balance);
    }
}
