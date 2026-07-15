namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Carries the current inbound channel through the backend execution scope.
/// The default keeps legacy in-process callers compatible.
/// </summary>
public static class BotChannelContext
{
    private static readonly AsyncLocal<BotChannel?> CurrentHolder = new();

    public static BotChannel Current => CurrentHolder.Value ?? BotChannel.Telegram;

    public static IDisposable Push(BotChannel channel)
    {
        var previous = CurrentHolder.Value;
        CurrentHolder.Value = channel;
        return new Scope(previous);
    }

    private sealed class Scope(BotChannel? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            CurrentHolder.Value = previous;
            _disposed = true;
        }
    }
}
