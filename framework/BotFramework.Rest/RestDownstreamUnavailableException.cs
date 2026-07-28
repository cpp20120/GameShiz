namespace BotFramework.Rest;

public sealed class RestDownstreamUnavailableException(string detail, Exception? inner = null)
    : RestHttpException(503, detail, "downstream_unavailable", TimeSpan.FromSeconds(1))
{
    public Exception? Inner { get; } = inner;
}
