
namespace Games.Challenges.Infrastructure.Modules;

using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Challenges.Application.Execution;
using Games.Challenges.Infrastructure.Configuration;

internal static class ChallengeExecutionRegistration
{
    public static IModuleServiceCollection AddChallengeExecution<TCommand, TResult, TAction, TDescriptor>(
        this IModuleServiceCollection services)
        where TCommand : IChallengeExecutionCommand
        where TAction : class, IGameAction<TCommand, ChallengeExecutionState, TResult>
        where TDescriptor : GameExecutionDescriptor<TCommand, ChallengeExecutionState, TResult> =>
        services
            .AddScoped<IGameAction<TCommand, ChallengeExecutionState, TResult>, TAction>()
            .AddScoped<GameExecutionDescriptor<TCommand, ChallengeExecutionState, TResult>, TDescriptor>()
            .AddScoped<IGameStateStore<TCommand, ChallengeExecutionState>, ChallengeExecutionStateStore<TCommand>>();
}
