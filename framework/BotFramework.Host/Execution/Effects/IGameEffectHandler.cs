using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

/// <summary>
/// Applies a materialized batch of module-defined effects inside the current
/// atomic game transaction without exposing the connection or transaction.
/// </summary>
public interface IGameEffectHandler
{
    Type EffectType { get; }

    int Order { get; }

    Task ApplyAsync(
        IReadOnlyList<IGameEffect> effects,
        IGameExecutionContext context,
        CancellationToken ct);
}
