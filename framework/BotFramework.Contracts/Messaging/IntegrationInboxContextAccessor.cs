namespace BotFramework.Contracts.Messaging;

public sealed class IntegrationInboxContextAccessor : IIntegrationInboxContextAccessor
{
    private readonly AsyncLocal<IntegrationInboxContext?> current = new();

    public IntegrationInboxContext? Current => current.Value;

    public IDisposable Push(IntegrationInboxContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = current.Value;
        current.Value = context;
        return new RestoreScope(this, previous);
    }

    private sealed class RestoreScope(IntegrationInboxContextAccessor owner, IntegrationInboxContext? previous)
        : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            owner.current.Value = previous;
            disposed = true;
        }
    }
}
