using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BotFramework.Rest;

internal sealed class RestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var (status, title, detail, code, retryAfter) = RestExceptionMapping.Map(exception);
        httpContext.Response.StatusCode = status;
        var retryAfterSeconds = retryAfter is { } retryValue
            ? Math.Max(1, (int)Math.Ceiling(retryValue.TotalSeconds))
            : (int?)null;
        if (retryAfterSeconds is { } seconds)
            httpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{status}",
                Extensions =
                {
                    ["code"] = code,
                    ["retryAfterSeconds"] = retryAfterSeconds,
                },
            },
        }).ConfigureAwait(false);
        return true;
    }
}

internal static class RestExceptionMapping
{
    public static (int Status, string Title, string Detail, string Code, TimeSpan? RetryAfter) Map(Exception exception)
    {
        if (exception is RestHttpException rest)
            return (rest.StatusCode, Title(rest.StatusCode), rest.Message, rest.Code, rest.RetryAfter);
        if (exception is ArgumentException or System.ComponentModel.DataAnnotations.ValidationException)
            return (400, "Request validation failed.", exception.Message, "validation_error", null);
        if (exception is KeyNotFoundException)
            return (404, "Resource not found.", exception.Message, "not_found", null);
        if (exception.GetType().Name is "GameStateConcurrencyException" or "ConcurrencyException")
            return (409, "State conflict.", exception.Message, "state_conflict", null);

        // gRPC keeps the status in Grpc.Core.RpcException. Use reflection here
        // so the reusable REST runtime does not take a dependency on gRPC.
        var statusCode = exception.GetType().GetProperty("StatusCode")?.GetValue(exception)?.ToString();
        if (statusCode is "Unavailable" or "DeadlineExceeded" or "ResourceExhausted")
            return (503, "Downstream service unavailable.", "A downstream service is temporarily unavailable.", "downstream_unavailable", TimeSpan.FromSeconds(1));
        if (statusCode is "NotFound")
            return (404, "Resource not found.", exception.Message, "not_found", null);
        if (statusCode is "PermissionDenied")
            return (403, "Forbidden.", exception.Message, "access_denied", null);
        if (statusCode is "InvalidArgument")
            return (400, "Request validation failed.", exception.Message, "validation_error", null);
        if (statusCode is "FailedPrecondition" or "Aborted" or "AlreadyExists")
            return (409, "State conflict.", exception.Message, "state_conflict", null);

        return (500, "Internal server error.", "An unexpected error occurred.", "internal_error", null);
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
