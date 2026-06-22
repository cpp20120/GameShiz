using BotFramework.Sdk;

namespace BotFramework.Host.Commands;

/// <summary>AsyncLocal shim that exposes the current request context to command middleware.</summary>
public static class RequestContextAccessor
{
    private static readonly AsyncLocal<RequestContext?> _current = new();

    public static RequestContext Current
    {
        get => _current.Value ?? Anonymous;
        set => _current.Value = value;
    }

    public static readonly RequestContext Anonymous = new(
        UserId: 0,
        CultureCode: "ru",
        TraceId: "00000000-0000-0000-0000-000000000000",
        Tags: new Dictionary<string, string>());
}
