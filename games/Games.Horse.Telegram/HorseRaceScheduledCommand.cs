using System.Globalization;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Scheduling.Abstractions;
using Games.Horse.Application.Services;
using Games.Horse.Domain.Configuration;

namespace Games.Horse.Telegram;

/// <summary>
/// Runs the configured race from the Telegram edge. The game backend settles
/// the race through gRPC, while this process owns Telegram delivery.
///
/// Quartz wakes the command once a minute so changes made from the admin UI
/// become effective without restarting the BFF. The actual cadence is
/// controlled by HorseOptions.AutoRunEveryDays and the configured local time.
/// </summary>
public sealed partial class HorseRaceScheduledCommand(
    IHorseService horse,
    IHorseRaceNotifier notifier,
    IRuntimeTuningAccessor tuning,
    TimeProvider timeProvider,
    ILogger<HorseRaceScheduledCommand> logger) : IRecurringScheduledCommand
{
    public const string CommandKey = "horse.run-daily-race";

    public string Key => CommandKey;

    // The command reloads the database overlay before each tick, so Quartz
    // itself does not need to be rebuilt when an admin changes the schedule.
    public ScheduleDescriptor Schedule => new(
        RepeatInterval: TimeSpan.FromMinutes(1),
        Policy: new ScheduleExecutionPolicy(
            Misfire: ScheduleMisfirePolicy.DoNothing,
            Concurrency: ScheduleConcurrencyPolicy.Disallow,
            MaxAttempts: 1));

    public async Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        await tuning.ReloadFromDatabaseAsync(ct).ConfigureAwait(false);
        var options = tuning.GetSection<HorseOptions>(HorseOptions.SectionName);
        if (!options.AutoRunEnabled || options.Admins.Count == 0)
            return;

        var localNow = timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(options.TimezoneOffsetHours));
        if (localNow.Hour != options.AutoRunLocalHour || localNow.Minute != options.AutoRunLocalMinute)
            return;

        var raceDate = GetRaceDate(options.TimezoneOffsetHours);
        if (!IsScheduledDay(raceDate, options.AutoRunEveryDays))
            return;

        // A Quartz misfire/restart can leave more than one invocation around
        // the target minute. The global result is the durable idempotency gate.
        if ((await horse.GetTodayResultAsync(0, ct).ConfigureAwait(false)).Winner is not null)
            return;

        var outcome = await horse.RunRaceAsync(options.Admins[0], HorseRunKind.Global, 0, ct)
            .ConfigureAwait(false);
        if (outcome.Error != HorseError.None)
        {
            LogSkipped(raceDate, outcome.Error);
            return;
        }

        await notifier.SendResultGifsAsync(outcome, raceDate, ct).ConfigureAwait(false);
        notifier.ScheduleWinnerAnnouncements(outcome);
        LogCompleted(raceDate, outcome.Winner + 1);
    }

    private static bool IsScheduledDay(string raceDate, int everyDays)
    {
        if (!DateOnly.TryParseExact(
                raceDate,
                "MM-dd-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return false;

        var period = Math.Max(1, everyDays);
        return (date.DayNumber - new DateOnly(2000, 1, 1).DayNumber) % period == 0;
    }

    private static string GetRaceDate(int timezoneOffsetHours)
    {
        var offset = TimeSpan.FromHours(timezoneOffsetHours);
        var local = DateTimeOffset.UtcNow.ToOffset(offset);
        return local.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
    }

    [LoggerMessage(EventId = 2860, Level = LogLevel.Information,
        Message = "horse.quartz completed race_date={RaceDate} winner={Winner}")]
    private partial void LogCompleted(string raceDate, int winner);

    [LoggerMessage(EventId = 2861, Level = LogLevel.Warning,
        Message = "horse.quartz skipped race_date={RaceDate} error={Error}")]
    private partial void LogSkipped(string raceDate, HorseError error);
}
