namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Carries transport metadata across the MediatR dispatch boundary without
/// contaminating every request DTO with delivery-specific fields.
/// </summary>
public static class RequestMetadataContext
{
    private static readonly AsyncLocal<Holder?> CurrentHolder = new();

    public static RequestMetadata Current => CurrentHolder.Value?.Value
        ?? throw new InvalidOperationException("Request metadata is unavailable outside an IRequestClient dispatch scope.");

    public static IDisposable Push(RequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var previous = CurrentHolder.Value;
        CurrentHolder.Value = new Holder(metadata);
        return new PopScope(previous);
    }

    private sealed record Holder(RequestMetadata Value);

    private sealed class PopScope(Holder? previous) : IDisposable
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
