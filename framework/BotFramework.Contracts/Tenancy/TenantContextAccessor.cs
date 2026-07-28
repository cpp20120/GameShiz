namespace BotFramework.Contracts.Tenancy;

/// <summary>Scoped accessor with safe restoration for nested fan-out.</summary>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private readonly AsyncLocal<TenantContext?> _current = new();

    public TenantContext? Current => _current.Value;

    public IDisposable Push(TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = _current.Value;
        _current.Value = context;
        return new RestoreScope(this, previous);
    }

    private sealed class RestoreScope(TenantContextAccessor owner, TenantContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            owner._current.Value = previous;
            _disposed = true;
        }
    }
}
