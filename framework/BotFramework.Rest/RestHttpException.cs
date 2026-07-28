namespace BotFramework.Rest;

public abstract class RestHttpException(
    int statusCode,
    string detail,
    string code = "http_error",
    TimeSpan? retryAfter = null) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
