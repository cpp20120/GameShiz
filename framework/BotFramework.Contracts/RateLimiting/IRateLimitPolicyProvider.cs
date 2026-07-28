namespace BotFramework.Contracts.RateLimiting;

/// <summary>
/// Resolves deployment defaults plus tenant/route overrides. Implementations
/// must not use raw URLs or player identifiers as policy keys.
/// </summary>
public interface IRateLimitPolicyProvider
{
    ValueTask<RateLimitPolicySet> ResolveAsync(
        RateLimitRequest request,
        RateLimitPolicySet deployment,
        CancellationToken cancellationToken = default);
}
