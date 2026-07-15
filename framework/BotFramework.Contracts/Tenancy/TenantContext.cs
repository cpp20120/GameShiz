using BotFramework.Contracts.Messaging;

namespace BotFramework.Contracts.Tenancy;

/// <summary>
/// Complete tenant boundary for one inbound operation.
/// </summary>
public sealed record TenantContext(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId? PlayerId,
    BotChannel Channel,
    RequestId RequestId,
    RequestId CorrelationId)
{
    /// <summary>Platform container used to provision a channel binding.</summary>
    public string? ChannelContainerId { get; init; }

    /// <summary>Platform topic/thread identifier used to provision a binding.</summary>
    public string? ChannelTopicId { get; init; }

    public bool HasPlayer => PlayerId.HasValue;

    public static TenantContext Create(
        TenantId tenantId,
        ScopeId scopeId,
        PlayerId? playerId,
        BotChannel channel,
        RequestId? requestId = null,
        RequestId? correlationId = null) =>
        new(
            tenantId,
            scopeId,
            playerId,
            channel,
            requestId ?? RequestId.New(),
            correlationId ?? requestId ?? RequestId.New());
}

/// <summary>Access to the current request's tenant boundary.</summary>
public interface ITenantContextAccessor
{
    TenantContext? Current { get; }

    TenantContext RequireCurrent() =>
        Current ?? throw new InvalidOperationException("Tenant context is unavailable outside an inbound operation.");

    IDisposable Push(TenantContext context);
}

/// <summary>
/// Ensures a resolved tenant/scope exists in the Host registry before a
/// request reaches module code. Implementations own only internal numeric
/// keys; SDK consumers see opaque identifiers exclusively.
/// </summary>
public interface ITenantContextProvisioner
{
    Task EnsureAsync(TenantContext context, CancellationToken cancellationToken = default);
}

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
