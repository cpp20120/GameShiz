namespace BotFramework.Host.Admin.Audit;

public sealed record AdminAuditRow(
    long Id,
    long ActorId,
    string ActorName,
    string Action,
    string DetailsJson,
    DateTimeOffset OccurredAt);
