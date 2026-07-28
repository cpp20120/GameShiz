using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Economics.Services;
using BotFramework.Sdk.Admin.Effects;

namespace BotFramework.Host.Admin.Effects;

internal sealed class RemoteWalletAdjustmentAdminEffectHandler(
    IEconomicsService economics) : AdminEffectHandler<WalletAdjustmentAdminEffect>
{
    protected override async Task ApplyAsync(
        WalletAdjustmentAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        if (effect.DisplayName is not null)
            await economics.EnsureUserAsync(effect.UserId, effect.BalanceScopeId, effect.DisplayName, ct);

        if (effect.Delta == 0)
        {
            context.SetOutput("balance", await economics.GetBalanceAsync(effect.UserId, effect.BalanceScopeId, ct));
            return;
        }

        var operationId = effect.OperationId ?? $"{context.Action}:wallet-adjust:{effect.UserId}:{effect.BalanceScopeId}";
        EconomicsMutationResult result;
        if (effect.Delta > 0)
        {
            result = await economics.CreditOnceAsync(
                effect.UserId, effect.BalanceScopeId, effect.Delta, effect.Reason, operationId, ct);
        }
        else if (effect.AllowNegative)
        {
            await economics.AdjustUncheckedAsync(effect.UserId, effect.BalanceScopeId, effect.Delta, ct);
            result = new EconomicsMutationResult(true, false,
                await economics.GetBalanceAsync(effect.UserId, effect.BalanceScopeId, ct));
        }
        else
        {
            result = await economics.TryDebitOnceAsync(
                effect.UserId, effect.BalanceScopeId, checked(-effect.Delta), effect.Reason, operationId, ct);
        }

        if (result.Rejected)
            throw new InsufficientFundsException(effect.UserId, effect.BalanceScopeId, checked(-effect.Delta), result.NewBalance);
        context.SetOutput("balance", result.NewBalance);
    }
}
