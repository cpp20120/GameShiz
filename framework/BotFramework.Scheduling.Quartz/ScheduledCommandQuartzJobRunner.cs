using System.Globalization;
using BotFramework.Scheduling.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace BotFramework.Scheduling.Quartz;

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

        var maxAttempts = Math.Max(1, ParseInt(context.MergedJobDataMap, "max-attempts", 1));
        var retryBackoffMs = Math.Max(0, ParseLong(context.MergedJobDataMap, "retry-backoff-ms", 0));
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

    private static int ParseInt(JobDataMap data, string key, int fallback) =>
        int.TryParse(data.GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static long ParseLong(JobDataMap data, string key, long fallback) =>
        long.TryParse(data.GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
}