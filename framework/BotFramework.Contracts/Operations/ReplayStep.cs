namespace BotFramework.Contracts.Operations;

public sealed record ReplayStep(long Version, string EventType, long OccurredAt,
    bool Compatible, string PayloadHash, string? Diagnostic);
