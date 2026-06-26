namespace BotFramework.Host.Commands.Dispatch;

/// <summary>AsyncLocal shim that exposes the current request context to command middleware.</summary>
public static class RequestContextAccessor
{
    private static readonly AsyncLocal<RequestContext?> CurrentContext = new();

    public static RequestContext Current
    {
        get => CurrentContext.Value ?? Anonymous;
        set => CurrentContext.Value = value;
    }

    public static readonly RequestContext Anonymous = new(
        UserId: 0,
        CultureCode: "ru",
        TraceId: "00000000-0000-0000-0000-000000000000",
        Tags: new Dictionary<string, string>(StringComparer.Ordinal));
}
