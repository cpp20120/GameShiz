using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace BotFramework.Scheduling.Quartz;

internal sealed class ConcurrentScheduledCommandQuartzJob(IServiceScopeFactory scopes) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        ScheduledCommandQuartzJobRunner.ExecuteAsync(scopes, context);
}