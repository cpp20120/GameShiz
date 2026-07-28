using BotFramework.Scheduling.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace BotFramework.Scheduling.Quartz;

public static class QuartzSchedulingExtensions
{
    public static IServiceCollection AddQuartzGameScheduling(
        this IServiceCollection services,
        string connectionString,
        string schedulerName = "CasinoShiz")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerName);
        schedulerName = NormalizeSchedulerName(schedulerName);
        services.AddQuartz(options =>
        {
            // Quartz uses sched_name as the logical scheduler partition in
            // the shared AdoJobStore tables. Replicas of one game use the
            // same name and cluster; different game services get different
            // names and cannot acquire each other's triggers.
            options.SchedulerName = schedulerName;
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

    private static string NormalizeSchedulerName(string value)
    {
        var normalized = new string(value
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "CasinoShiz" : normalized;
    }

    public static IServiceCollection AddQuartzRecurringCommandBootstrapper(this IServiceCollection services)
    {
        services.AddHostedService<QuartzRecurringCommandBootstrapper>();
        return services;
    }
}