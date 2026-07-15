namespace BotFramework.Rest;

public abstract class RestHttpException(int statusCode, string detail, string code = "http_error", TimeSpan? retryAfter = null) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public sealed class RestBadRequestException(string detail, string code = "validation_error") : RestHttpException(400, detail, code);

public sealed class RestUnauthorizedException(string detail, string code = "authentication_required") : RestHttpException(401, detail, code);

public sealed class RestForbiddenException(string detail, string code = "access_denied") : RestHttpException(403, detail, code);

public sealed class RestNotFoundException(string detail, string code = "not_found") : RestHttpException(404, detail, code);

public sealed class RestConflictException(string detail, string code = "conflict") : RestHttpException(409, detail, code);

public sealed class RestDownstreamUnavailableException(string detail, Exception? inner = null)
    : RestHttpException(503, detail, "downstream_unavailable", TimeSpan.FromSeconds(1))
{
    public Exception? Inner { get; } = inner;
}
