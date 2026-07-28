using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Host.Admin.Execution;

public interface IAdminEffectExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        AdminExecutionEnvelope envelope,
        AdminEffectPlan<TResult> plan,
        CancellationToken ct);
}
