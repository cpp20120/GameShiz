using BotFramework.Scheduling.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BotFramework.Scheduling.Quartz;

internal sealed class QuartzRecurringCommandBootstrapper(
    IServiceScopeFactory scopes,
    IGameScheduler scheduler) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetServices<IRecurringScheduledCommand>();
        foreach (var command in commands)
        {
            await scheduler.ScheduleAsync(new GameScheduleCommand(
                ScheduleId: command.Key,
                JobKey: command.Key,
                Schedule: command.Schedule), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}