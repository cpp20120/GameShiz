using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Economics.Services;
using BotFramework.Sdk.Admin.Effects;

namespace BotFramework.Host.Admin.Effects;

internal sealed class RemoteWalletSetAdminEffectHandler(
    IEconomicsService economics) : AdminEffectHandler<WalletSetAdminEffect>
{
    protected override async Task ApplyAsync(
        WalletSetAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        if (effect.DisplayName is not null)
            await economics.EnsureUserAsync(effect.UserId, effect.BalanceScopeId, effect.DisplayName, ct);

        var current = await economics.GetBalanceAsync(effect.UserId, effect.BalanceScopeId, ct);
        var delta = checked(effect.Balance - current);
        if (delta == 0)
        {
            context.SetOutput("balance", current);
            return;
        }
        if (effect is { AllowNegative: false, Balance: < 0 })
            throw new InsufficientFundsException(effect.UserId, effect.BalanceScopeId, -effect.Balance, current);

        var operationId = effect.OperationId ?? $"{context.Action}:wallet-set:{effect.UserId}:{effect.BalanceScopeId}";
        if (delta > 0)
        {
            _ = await economics.CreditOnceAsync(effect.UserId, effect.BalanceScopeId, delta, effect.Reason, operationId, ct);
        }
        else if (effect.AllowNegative)
        {
            await economics.AdjustUncheckedAsync(effect.UserId, effect.BalanceScopeId, delta, ct);
        }
        else
        {
            var result = await economics.TryDebitOnceAsync(
                effect.UserId, effect.BalanceScopeId, -delta, effect.Reason, operationId, ct);
            if (result.Rejected)
                throw new InsufficientFundsException(effect.UserId, effect.BalanceScopeId, -delta, result.NewBalance);
        }

        context.SetOutput("balance", effect.Balance);
    }
}
