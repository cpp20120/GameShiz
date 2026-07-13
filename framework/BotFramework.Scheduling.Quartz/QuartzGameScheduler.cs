using BotFramework.Scheduling.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl.Matchers;

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
                store.UseSystemTextJsonSerializer();
            });
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.AddSingleton<QuartzGameScheduler>();
        services.AddSingleton<IGameScheduler>(sp => sp.GetRequiredService<QuartzGameScheduler>());
        services.AddSingleton<IGameSchedulerStatusReader>(sp => sp.GetRequiredService<QuartzGameScheduler>());
        return services;
    }

    public static IServiceCollection AddQuartzRecurringCommandBootstrapper(this IServiceCollection services)
    {
        services.AddHostedService<QuartzRecurringCommandBootstrapper>();
        return services;
    }
}

internal sealed class QuartzGameScheduler(ISchedulerFactory schedulers) : IGameScheduler, IGameSchedulerStatusReader
{
    public async Task ScheduleAsync(GameScheduleCommand command, CancellationToken ct)
    {
        var scheduler = await schedulers.GetScheduler(ct);
        var jobKey = new JobKey(command.JobKey, "games");
        var jobType = command.Schedule.EffectivePolicy.Concurrency == ScheduleConcurrencyPolicy.Allow
            ? typeof(ConcurrentScheduledCommandQuartzJob)
            : typeof(ScheduledCommandQuartzJob);
        var job = JobBuilder.Create(jobType)
            .WithIdentity(jobKey)
            .UsingJobData("command-key", command.JobKey)
            .UsingJobData("max-attempts", Math.Max(1, command.Schedule.EffectivePolicy.MaxAttempts))
            .UsingJobData("retry-backoff-ms", (long)Math.Max(0, command.Schedule.EffectivePolicy.EffectiveRetryBackoff.TotalMilliseconds))
            .UsingJobData("batch-size", Math.Max(1, command.Schedule.EffectivePolicy.BatchSize))
            .StoreDurably()
            .Build();
        foreach (var pair in command.Data ?? new Dictionary<string, string>(StringComparer.Ordinal))
            job.JobDataMap[pair.Key] = pair.Value;
        await scheduler.AddJob(job, true, true, ct);

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity(command.ScheduleId, "games")
            .ForJob(jobKey);
        triggerBuilder = command.Schedule switch
        {
            { RunAt: { } runAt } => triggerBuilder.StartAt(runAt),
            { CronExpression: { Length: > 0 } cronExpression } =>
                triggerBuilder.WithSchedule(BuildCronSchedule(
                    cronExpression,
                    command.Schedule.TimeZoneId,
                    command.Schedule.EffectivePolicy.Misfire)),
            { RepeatInterval: { } interval } when interval > TimeSpan.Zero =>
                triggerBuilder.WithSchedule(BuildSimpleSchedule(interval, command.Schedule.EffectivePolicy.Misfire)),
            _ => throw new ArgumentException("A cron expression or positive repeat interval is required.", nameof(command)),
        };
        var triggerKey = new TriggerKey(command.ScheduleId, "games");
        var trigger = triggerBuilder.Build();
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

    public async Task<IReadOnlyList<GameScheduledJobStatus>> SnapshotAsync(CancellationToken ct)
    {
        var scheduler = await schedulers.GetScheduler(ct);
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("games"), ct);
        var result = new List<GameScheduledJobStatus>();
        foreach (var jobKey in jobKeys.OrderBy(static key => key.Name, StringComparer.Ordinal))
        {
            var triggers = await scheduler.GetTriggersOfJob(jobKey, ct);
            foreach (var trigger in triggers.OrderBy(static item => item.Key.Name, StringComparer.Ordinal))
            {
                var state = await scheduler.GetTriggerState(trigger.Key, ct);
                result.Add(new GameScheduledJobStatus(
                    trigger.Key.Name,
                    jobKey.Name,
                    state.ToString().ToLowerInvariant(),
                    trigger.GetPreviousFireTimeUtc(),
                    trigger.GetNextFireTimeUtc()));
            }
        }

        return result;
    }

    private static CronScheduleBuilder BuildCronSchedule(
        string cronExpression,
        string? timeZoneId,
        ScheduleMisfirePolicy misfire)
    {
        var cron = CronScheduleBuilder.CronSchedule(cronExpression);
        cron = misfire switch
        {
            ScheduleMisfirePolicy.Ignore => cron.WithMisfireHandlingInstructionIgnoreMisfires(),
            ScheduleMisfirePolicy.DoNothing => cron.WithMisfireHandlingInstructionDoNothing(),
            _ => cron.WithMisfireHandlingInstructionFireAndProceed(),
        };
        if (!string.IsNullOrWhiteSpace(timeZoneId))
            cron.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        return cron;
    }

    private static SimpleScheduleBuilder BuildSimpleSchedule(TimeSpan interval, ScheduleMisfirePolicy misfire)
    {
        var schedule = SimpleScheduleBuilder.RepeatSecondlyForever(1).WithInterval(interval);
        return misfire switch
        {
            ScheduleMisfirePolicy.Ignore => schedule.WithMisfireHandlingInstructionIgnoreMisfires(),
            ScheduleMisfirePolicy.DoNothing => schedule.WithMisfireHandlingInstructionNextWithExistingCount(),
            _ => schedule.WithMisfireHandlingInstructionFireNow(),
        };
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

internal static class ScheduledCommandQuartzJobRunner
{
    public static async Task ExecuteAsync(IServiceScopeFactory scopes, IJobExecutionContext context)
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

        var maxAttempts = Math.Max(1, context.MergedJobDataMap.GetInt("max-attempts"));
        var retryBackoffMs = Math.Max(0, context.MergedJobDataMap.GetLong("retry-backoff-ms"));
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await command.ExecuteAsync(data, context.CancellationToken);
                return;
            }
            catch when (attempt < maxAttempts)
            {
                if (retryBackoffMs > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(retryBackoffMs * attempt), context.CancellationToken);
            }
        }

        throw new InvalidOperationException("Scheduled command execution exhausted its retry policy.");
    }
}

[DisallowConcurrentExecution]
internal sealed class ScheduledCommandQuartzJob(IServiceScopeFactory scopes) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        ScheduledCommandQuartzJobRunner.ExecuteAsync(scopes, context);
}

internal sealed class ConcurrentScheduledCommandQuartzJob(IServiceScopeFactory scopes) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        ScheduledCommandQuartzJobRunner.ExecuteAsync(scopes, context);
}
