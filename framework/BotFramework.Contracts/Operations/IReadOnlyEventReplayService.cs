namespace BotFramework.Contracts.Operations;

public interface IReadOnlyEventReplayService
{
    Task<EventReplayReport> ReplayAsync(string streamId, CancellationToken ct = default);
}
