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

        var (status, title, detail) = RestExceptionMapping.Map(exception);
        httpContext.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{status}",
            },
        }).ConfigureAwait(false);
        return true;
    }
}

internal static class RestExceptionMapping
{
    public static (int Status, string Title, string Detail) Map(Exception exception)
    {
        if (exception is RestHttpException rest)
            return (rest.StatusCode, Title(rest.StatusCode), rest.Message);
        if (exception is ArgumentException or System.ComponentModel.DataAnnotations.ValidationException)
            return (400, "Request validation failed.", exception.Message);
        if (exception is KeyNotFoundException)
            return (404, "Resource not found.", exception.Message);
        if (exception.GetType().Name is "GameStateConcurrencyException" or "ConcurrencyException")
            return (409, "State conflict.", exception.Message);

        // gRPC keeps the status in Grpc.Core.RpcException. Use reflection here
        // so the reusable REST runtime does not take a dependency on gRPC.
        var statusCode = exception.GetType().GetProperty("StatusCode")?.GetValue(exception)?.ToString();
        if (statusCode is "Unavailable" or "DeadlineExceeded" or "ResourceExhausted")
            return (503, "Downstream service unavailable.", "A downstream service is temporarily unavailable.");
        if (statusCode is "NotFound")
            return (404, "Resource not found.", exception.Message);
        if (statusCode is "PermissionDenied")
            return (403, "Forbidden.", exception.Message);
        if (statusCode is "InvalidArgument")
            return (400, "Request validation failed.", exception.Message);
        if (statusCode is "FailedPrecondition" or "Aborted" or "AlreadyExists")
            return (409, "State conflict.", exception.Message);

        return (500, "Internal server error.", "An unexpected error occurred.");
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
