namespace BotFramework.Host.Events;

public interface IEventReplayService
{
    IReadOnlyList<ProjectionDescriptor> ListRebuildableProjections();

    Task<ProjectionReplayResult> RebuildProjectionAsync(
        string projectionName,
        CancellationToken ct);
}
