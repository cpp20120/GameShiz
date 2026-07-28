namespace BotFramework.Contracts.Operations;

public sealed record OperationAudit(long Id, long ActorId, string ActorName, string Action,
    string DetailsJson, DateTimeOffset OccurredAt);
