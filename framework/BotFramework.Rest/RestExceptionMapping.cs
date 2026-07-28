namespace BotFramework.Rest;

internal static class RestExceptionMapping
{
    public static (int Status, string Title, string Detail, string Code, TimeSpan? RetryAfter) Map(Exception exception)
    {
        switch (exception)
        {
            case RestHttpException rest:
                return (rest.StatusCode, Title(rest.StatusCode), rest.Message, rest.Code, rest.RetryAfter);
            case ArgumentException or System.ComponentModel.DataAnnotations.ValidationException:
                return (400, "Request validation failed.", exception.Message, "validation_error", null);
            case KeyNotFoundException:
                return (404, "Resource not found.", exception.Message, "not_found", null);
        }

        if (exception.GetType().Name is "GameStateConcurrencyException" or "ConcurrencyException")
            return (409, "State conflict.", exception.Message, "state_conflict", null);

        // gRPC keeps the status in Grpc.Core.RpcException. Use reflection here
        // so the reusable REST runtime does not take a dependency on gRPC.
        var statusCode = exception.GetType().GetProperty("StatusCode")?.GetValue(exception)?.ToString();
        return statusCode switch
        {
            "Unavailable" or "DeadlineExceeded" or "ResourceExhausted" => (503, "Downstream service unavailable.",
                "A downstream service is temporarily unavailable.", "downstream_unavailable", TimeSpan.FromSeconds(1)),
            "NotFound" => (404, "Resource not found.", exception.Message, "not_found", null),
            "PermissionDenied" => (403, "Forbidden.", exception.Message, "access_denied", null),
            "InvalidArgument" => (400, "Request validation failed.", exception.Message, "validation_error", null),
            "FailedPrecondition" or "Aborted" or "AlreadyExists" => (409, "State conflict.", exception.Message,
                "state_conflict", null),
            _ => (500, "Internal server error.", "An unexpected error occurred.", "internal_error", null),
        };
    }

    private static string Title(int status) => status switch
    {
        400 => "Request validation failed.",
        401 => "Authentication required.",
        403 => "Forbidden.",
        404 => "Resource not found.",
        409 => "State conflict.",
        429 => "Rate limit exceeded.",
        503 => "Downstream service unavailable.",
        _ => "Request failed.",
    };
}
