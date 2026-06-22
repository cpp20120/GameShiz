namespace BotFramework.Host.Events.Replay;

public interface IEventReplayService
{
    IReadOnlyList<ProjectionDescriptor> ListRebuildableProjections();

    Task<ProjectionReplayResult> RebuildProjectionAsync(
        string projectionName,
        CancellationToken ct);
}
