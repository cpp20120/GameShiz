using BotFramework.Scheduling.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Games.Horse.Application.Jobs;

public sealed class HorseRaceSchedulerService(
    IGameScheduler scheduler,
    IOptions<HorseOptions> options) : IBackgroundJob
{
    public string Name => "horse.race_scheduler";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        if (value.AutoRunEnabled)
        {
            var utcHour = (Math.Clamp(value.AutoRunLocalHour, 0, 23) - value.TimezoneOffsetHours + 24) % 24;
            var minute = Math.Clamp(value.AutoRunLocalMinute, 0, 59);
            await scheduler.ScheduleAsync(new(
                "horse.daily-race",
                HorseRaceScheduledCommand.CommandKey,
                new ScheduleDescriptor($"0 {minute} {utcHour} * * ?")), stoppingToken);
        }
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}

public sealed partial class HorseRaceScheduledCommand(
    IHorseService horse,
    IHorseRaceNotifier notifier,
    IHorseResultStore results,
    IOptions<HorseOptions> options,
    ILogger<HorseRaceScheduledCommand> logger) : IScheduledCommand
{
    public const string CommandKey = "horse.run-daily-race";
    public string Key => CommandKey;

    public async Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        var value = options.Value;
        if (!value.AutoRunEnabled || value.Admins.Count == 0) return;
        var raceDate = HorseTimeHelper.GetRaceDate(value.TimezoneOffsetHours);
        if (await results.FindAsync(raceDate, 0, ct) is not null) return;

        var outcome = await horse.RunRaceAsync(value.Admins[0], HorseRunKind.Global, 0, ct);
        if (outcome.Error != HorseError.None)
        {
            LogSkipped(raceDate, outcome.Error);
            return;
        }
        await notifier.SendResultGifsAsync(outcome, raceDate, ct);
        notifier.ScheduleWinnerAnnouncements(outcome);
        LogCompleted(raceDate, outcome.Winner + 1);
    }

    [LoggerMessage(EventId = 2860, Level = LogLevel.Information, Message = "horse.quartz completed race_date={RaceDate} winner={Winner}")]
    private partial void LogCompleted(string raceDate, int winner);

    [LoggerMessage(EventId = 2861, Level = LogLevel.Warning, Message = "horse.quartz skipped race_date={RaceDate} error={Error}")]
    private partial void LogSkipped(string raceDate, HorseError error);
}
