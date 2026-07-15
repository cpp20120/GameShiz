using Microsoft.Extensions.Configuration;

namespace CasinoShiz.ServiceDefaults;

/// <summary>
/// Resolves an optional per-game backend endpoint while retaining the single
/// Backend:GrpcAddress fallback used by the monolith and legacy microservices
/// Compose profile.
/// </summary>
public static class BackendAddressResolver
{
    public static Uri ResolveGameAddress(
        this IConfiguration configuration,
        string gameId,
        Uri fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(fallback);

        var configured = configuration[$"Backend:GameAddresses:{gameId}"];
        return Uri.TryCreate(configured, UriKind.Absolute, out var address)
            ? address
            : fallback;
    }
}
