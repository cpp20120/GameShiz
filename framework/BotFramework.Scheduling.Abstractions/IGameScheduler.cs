namespace BotFramework.Scheduling.Abstractions;

public interface IGameScheduler
{
    Task ScheduleAsync(GameScheduleCommand command, CancellationToken ct);
    Task TriggerNowAsync(string jobKey, IReadOnlyDictionary<string, string> data, CancellationToken ct);
    Task UnscheduleAsync(string scheduleId, CancellationToken ct);
}
