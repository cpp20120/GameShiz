using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Economics.Services;
using BotFramework.Sdk.Admin.Effects;

namespace BotFramework.Host.Admin.Effects;

internal sealed class RemoteLedgerRevertAdminEffectHandler(
    IEconomicsService economics) : AdminEffectHandler<LedgerRevertAdminEffect>
{
    protected override async Task ApplyAsync(
        LedgerRevertAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        context.SetOutput("result", await economics.RevertLedgerEntryAsync(effect.LedgerId, ct));
    }
}
