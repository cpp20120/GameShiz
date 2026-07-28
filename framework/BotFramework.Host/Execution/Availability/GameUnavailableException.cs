namespace BotFramework.Host.Execution;

public sealed class GameUnavailableException : Exception
{
    public GameUnavailableException()
        : this(string.Empty, 0, null)
    {
    }

    public GameUnavailableException(string message)
        : base(message)
    {
    }

    public GameUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GameUnavailableException(string gameId, long chatId, string? reason)
        : base(reason is null ? $"Game '{gameId}' is disabled." : $"Game '{gameId}' is disabled: {reason}")
    {
        GameId = gameId;
        ChatId = chatId;
        Reason = reason;
    }

    public string GameId { get; } = string.Empty;

    public long ChatId { get; }

    public string? Reason { get; }
}
