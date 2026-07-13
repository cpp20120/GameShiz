using BotFramework.Sdk.Execution;
using BotFramework.Scheduling.Abstractions;

namespace BotFramework.Host.Execution;

public static class TurnBasedGameRegistrationExtensions
{
    /// <summary>
    /// Registers a turn action with framework-owned JSONB versioned state persistence.
    /// A game can replace IGameStateStore after this call when it needs a relational model.
    /// </summary>
    public static IServiceCollection AddAtomicTurnBasedGameAction<
        TCommand,
        TState,
        TAction,
        TResult,
        TDescriptor>(this IServiceCollection services)
        where TState : class, IVersionedGameState
        where TAction : class, IGameAction<TCommand, TState, TResult>
        where TDescriptor : GameExecutionDescriptor<TCommand, TState, TResult>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TDescriptor>();
        services.AddScoped<GameExecutionDescriptor<TCommand, TState, TResult>>(
            provider => provider.GetRequiredService<TDescriptor>());
        services.AddScoped<IGameAction<TCommand, TState, TResult>, TAction>();
        services.AddScoped<IGameStateStore<TCommand, TState>,
            PostgresJsonGameStateStore<TCommand, TState, TResult>>();
        services.AddScoped<IScheduledCommand,
            AtomicGameScheduledCommand<TCommand, TState, TResult>>();
        return services;
    }
}
