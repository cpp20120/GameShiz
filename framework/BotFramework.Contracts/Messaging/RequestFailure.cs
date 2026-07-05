namespace BotFramework.Contracts.Messaging;

public sealed record RequestFailure(
    string Code,
    string Message,
    bool IsTransient = false,
    IReadOnlyDictionary<string, string>? Details = null);
