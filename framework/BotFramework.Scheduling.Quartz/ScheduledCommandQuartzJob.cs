using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace BotFramework.Scheduling.Quartz;

[DisallowConcurrentExecution]
internal sealed class ScheduledCommandQuartzJob(IServiceScopeFactory scopes) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        ScheduledCommandQuartzJobRunner.ExecuteAsync(scopes, context);
}