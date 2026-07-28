using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Effects;

namespace BotFramework.Host.Admin.Effects;

internal sealed class TenantWalletSetAdminEffectHandler
    : AdminEffectHandler<TenantWalletSetAdminEffect>
{
    protected override async Task ApplyAsync(
        TenantWalletSetAdminEffect effect,
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
        if (!effect.AllowNegative && effect.Balance < 0)
            throw new InvalidOperationException("The tenant wallet cannot become negative.");

        var delta = checked(effect.Balance - row.Balance);
        if (delta != 0)
        {
            await TenantWalletAdminSql.ApplyAsync(
                effect.TenantId,
                effect.ScopeId,
                effect.PlayerId,
                effect.Balance,
                checked(row.Version + 1),
                delta,
                effect.Reason,
                effect.OperationId,
                context,
                ct);
        }
        context.SetOutput("balance", effect.Balance);
    }
}
