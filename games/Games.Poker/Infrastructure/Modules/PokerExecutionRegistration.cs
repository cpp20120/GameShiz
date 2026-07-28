
namespace Games.Poker.Infrastructure.Modules;

using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Poker.Application.Execution;
using Games.Poker.Infrastructure.Configuration;

internal static class PokerExecutionRegistration
{
    public static IModuleServiceCollection AddPokerExecution<TCommand, TResult, TAction, TDescriptor>(
        this IModuleServiceCollection services)
        where TCommand : IPokerExecutionCommand
        where TAction : class, IGameAction<TCommand, PokerExecutionState, TResult>
        where TDescriptor : GameExecutionDescriptor<TCommand, PokerExecutionState, TResult> =>
        services
            .AddScoped<IGameAction<TCommand, PokerExecutionState, TResult>, TAction>()
            .AddScoped<GameExecutionDescriptor<TCommand, PokerExecutionState, TResult>, TDescriptor>()
            .AddScoped<IGameStateStore<TCommand, PokerExecutionState>, PokerExecutionStateStore<TCommand>>();
}
