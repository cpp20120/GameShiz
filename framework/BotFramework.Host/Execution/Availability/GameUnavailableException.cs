namespace BotFramework.Host.Execution;

public sealed class GameUnavailableException(string gameId, long chatId, string? reason)
    : Exception(reason is null ? $"Game '{gameId}' is disabled." : $"Game '{gameId}' is disabled: {reason}")
{
    public string GameId { get; } = gameId;

    public long ChatId { get; } = chatId;

    public string? Reason { get; } = reason;
}
