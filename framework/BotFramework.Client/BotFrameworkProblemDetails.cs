namespace BotFramework.Client;

public sealed record BotFrameworkProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    string? Code,
    string? CorrelationId,
    int? RetryAfterSeconds);
