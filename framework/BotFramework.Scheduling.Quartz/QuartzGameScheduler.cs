using BotFramework.Scheduling.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace BotFramework.Scheduling.Quartz;

public static class QuartzSchedulingExtensions
{
    public static IServiceCollection AddQuartzGameScheduling(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddQuartz(options =>
        {
            options.SchedulerName = "CasinoShiz";
            options.SchedulerId = "AUTO";
            options.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UsePostgres(connectionString);
                store.UseClustering();
            });
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.AddSingleton<IGameScheduler, QuartzGameScheduler>();
        return services;
    }

    public static IServiceCollection AddQuartzRecurringCommandBootstrapper(this IServiceCollection services)
    {
        services.AddHostedService<QuartzRecurringCommandBootstrapper>();
        return services;
    }
}

internal sealed class QuartzGameScheduler(ISchedulerFactory schedulers) : IGameScheduler
{
    public async Task ScheduleAsync(GameScheduleCommand command, CancellationToken ct)
    {
        var scheduler = await schedulers.GetScheduler(ct);
        var jobKey = new JobKey(command.JobKey, "games");
        var job = JobBuilder.Create<ScheduledCommandQuartzJob>()
            .WithIdentity(jobKey)
            .UsingJobData("command-key", command.JobKey)
            .StoreDurably()
            .Build();
        foreach (var pair in command.Data ?? new Dictionary<string, string>(StringComparer.Ordinal))
            job.JobDataMap[pair.Key] = pair.Value;
        await scheduler.AddJob(job, true, true, ct);

        IScheduleBuilder schedule = command.Schedule switch
        {
            { CronExpression: { Length: > 0 } cronExpression } => BuildCronSchedule(cronExpression, command.Schedule.TimeZoneId),
            { RepeatInterval: { } interval } when interval > TimeSpan.Zero =>
                SimpleScheduleBuilder.RepeatSecondlyForever(1).WithInterval(interval),
            _ => throw new ArgumentException("A cron expression or positive repeat interval is required.", nameof(command)),
        };
        var triggerKey = new TriggerKey(command.ScheduleId, "games");
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithSchedule(schedule)
            .Build();
        if (await scheduler.CheckExists(triggerKey, ct))
            await scheduler.RescheduleJob(triggerKey, trigger, ct);
        else
        {
            try
            {
                await scheduler.ScheduleJob(trigger, ct);
            }
            catch (ObjectAlreadyExistsException)
            {
                await scheduler.RescheduleJob(triggerKey, trigger, ct);
            }
        }
    }

    public async Task TriggerNowAsync(string jobKey, IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        var scheduler = await schedulers.GetScheduler(ct);
        var map = new JobDataMap();
        foreach (var pair in data) map[pair.Key] = pair.Value;
        await scheduler.TriggerJob(new JobKey(jobKey, "games"), map, ct);
    }

    public async Task UnscheduleAsync(string scheduleId, CancellationToken ct)
    {
        var scheduler = await schedulers.GetScheduler(ct);
        await scheduler.UnscheduleJob(new TriggerKey(scheduleId, "games"), ct);
    }

    private static CronScheduleBuilder BuildCronSchedule(string cronExpression, string? timeZoneId)
    {
        var cron = CronScheduleBuilder.CronSchedule(cronExpression)
            .WithMisfireHandlingInstructionFireAndProceed();
        if (!string.IsNullOrWhiteSpace(timeZoneId))
            cron.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        return cron;
    }
}

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

[DisallowConcurrentExecution]
internal sealed class ScheduledCommandQuartzJob(IServiceScopeFactory scopes) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopes.CreateAsyncScope();
        var key = context.MergedJobDataMap.GetString("command-key")
            ?? throw new InvalidOperationException("Scheduled command key is missing.");
        var command = scope.ServiceProvider.GetServices<IScheduledCommand>()
            .SingleOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Scheduled command '{key}' is not registered.");
        var data = context.MergedJobDataMap.Keys
            .Where(item => !string.Equals(item, "command-key", StringComparison.Ordinal))
            .ToDictionary(item => item, item => context.MergedJobDataMap.GetString(item) ?? "", StringComparer.Ordinal);
        await command.ExecuteAsync(data, context.CancellationToken);
    }
}
