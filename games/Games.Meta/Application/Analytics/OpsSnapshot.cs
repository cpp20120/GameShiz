namespace Games.Meta.Application.Analytics;

internal sealed record OpsSnapshot(
    long UnresolvedDispatchFailures,
    long KnownChats,
    long ProcessedUpdates,
    long AdminActions);
