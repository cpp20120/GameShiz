namespace Games.Meta;

internal sealed record OpsSnapshot(
    long UnresolvedDispatchFailures,
    long KnownChats,
    long ProcessedUpdates,
    long AdminActions);
