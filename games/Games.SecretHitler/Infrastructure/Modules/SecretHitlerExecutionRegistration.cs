
namespace Games.SecretHitler.Infrastructure.Modules;

using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.SecretHitler.Application.Execution;
using Games.SecretHitler.Infrastructure.Configuration;

internal static class SecretHitlerExecutionRegistration
{
    public static IModuleServiceCollection AddShExecution<TCommand, TResult, TAction, TDescriptor>(
        this IModuleServiceCollection services)
        where TCommand : ISecretHitlerExecutionCommand
        where TAction : class, IGameAction<TCommand, SecretHitlerExecutionState, TResult>
        where TDescriptor : GameExecutionDescriptor<TCommand, SecretHitlerExecutionState, TResult> =>
        services
            .AddScoped<IGameAction<TCommand, SecretHitlerExecutionState, TResult>, TAction>()
            .AddScoped<GameExecutionDescriptor<TCommand, SecretHitlerExecutionState, TResult>, TDescriptor>()
            .AddScoped<IGameStateStore<TCommand, SecretHitlerExecutionState>,
                SecretHitlerExecutionStateStore<TCommand>>();
}
