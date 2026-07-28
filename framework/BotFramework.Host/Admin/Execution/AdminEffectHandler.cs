using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Host.Admin.Execution;

public abstract class AdminEffectHandler<TEffect> : IAdminEffectHandler
    where TEffect : class, IAdminEffect
{
    public Type EffectType => typeof(TEffect);

    public Task ApplyAsync(IAdminEffect effect, IAdminExecutionContext context, CancellationToken ct) =>
        ApplyAsync((TEffect)effect, context, ct);

    protected abstract Task ApplyAsync(TEffect effect, IAdminExecutionContext context, CancellationToken ct);
}
