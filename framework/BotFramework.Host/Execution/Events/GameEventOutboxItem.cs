namespace BotFramework.Host.Execution;

internal sealed record GameEventOutboxItem(
    long Id,
    string TypeName,
    string Payload,
    int Attempts,
    DateTimeOffset CreatedAt);
