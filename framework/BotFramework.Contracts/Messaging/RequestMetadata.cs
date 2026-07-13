namespace BotFramework.Contracts.Messaging;

public sealed record RequestMetadata(
    string RequestId,
    string CorrelationId,
    string ClientId,
    string? UserId,
    string? ScopeId,
    string Culture,
    IReadOnlyDictionary<string, string> Baggage)
{
    public static RequestMetadata Create(
        string clientId,
        string? userId = null,
        string? scopeId = null,
        string culture = "en")
    {
        var requestId = Guid.NewGuid().ToString("N");
        return new RequestMetadata(
            requestId,
            requestId,
            clientId,
            userId,
            scopeId,
            culture,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
