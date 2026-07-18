using BotFramework.Scheduling.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Games.Horse.Application.Jobs;

/// <summary>
/// Removes the old backend-owned race trigger after scheduled delivery moved
/// to the Telegram edge. The cleanup is registered by the backend host only;
/// the monolith keeps the same key and schedules the Telegram command locally.
/// </summary>
public sealed partial class HorseLegacyScheduleCleanup(
    IGameScheduler scheduler,
    ILogger<HorseLegacyScheduleCleanup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await scheduler.UnscheduleAsync(HorseRaceScheduledCommand.CommandKey, cancellationToken)
                .ConfigureAwait(false);
            LogRemoved();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCleanupFailed(exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 2862, Level = LogLevel.Information,
        Message = "horse.quartz removed legacy backend-owned schedule")]
    private partial void LogRemoved();

    [LoggerMessage(EventId = 2863, Level = LogLevel.Warning,
        Message = "horse.quartz legacy schedule cleanup failed")]
    private partial void LogCleanupFailed(Exception exception);
}
