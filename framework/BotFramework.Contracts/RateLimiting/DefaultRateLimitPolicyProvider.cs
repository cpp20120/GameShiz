namespace BotFramework.Contracts.RateLimiting;

public sealed class DefaultRateLimitPolicyProvider : IRateLimitPolicyProvider
{
    public ValueTask<RateLimitPolicySet> ResolveAsync(
        RateLimitRequest request,
        RateLimitPolicySet deployment,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(deployment);
}
