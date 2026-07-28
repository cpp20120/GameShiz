using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Host.Admin.Execution;

public interface IAdminEffectHandler
{
    Type EffectType { get; }
    Task ApplyAsync(IAdminEffect effect, IAdminExecutionContext context, CancellationToken ct);
}
