using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

public interface IAtomicEffectHandler
{
    Type EffectType { get; }

    Task ApplyAsync(IAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct);
}