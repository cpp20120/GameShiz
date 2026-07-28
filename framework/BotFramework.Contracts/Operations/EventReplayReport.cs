namespace BotFramework.Contracts.Operations;

public sealed record EventReplayReport(string StreamId, IReadOnlyList<ReplayStep> Steps,
    long? FirstIncompatibleVersion, string? Diagnostic);
