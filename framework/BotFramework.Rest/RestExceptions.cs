namespace BotFramework.Rest;

public abstract class RestHttpException(int statusCode, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class RestBadRequestException(string detail) : RestHttpException(400, detail);

public sealed class RestUnauthorizedException(string detail) : RestHttpException(401, detail);

public sealed class RestForbiddenException(string detail) : RestHttpException(403, detail);

public sealed class RestNotFoundException(string detail) : RestHttpException(404, detail);

public sealed class RestConflictException(string detail) : RestHttpException(409, detail);

public sealed class RestDownstreamUnavailableException(string detail, Exception? inner = null)
    : RestHttpException(503, detail)
{
    public Exception? Inner { get; } = inner;
}
