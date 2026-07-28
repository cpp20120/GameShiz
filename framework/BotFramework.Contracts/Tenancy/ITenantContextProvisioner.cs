namespace BotFramework.Contracts.Tenancy;

/// <summary>
/// Ensures a resolved tenant/scope exists in the Host registry before a
/// request reaches module code. Implementations own only internal numeric
/// keys; SDK consumers see opaque identifiers exclusively.
/// </summary>
public interface ITenantContextProvisioner
{
    Task EnsureAsync(TenantContext context, CancellationToken cancellationToken = default);
}
