using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BotFramework.Rest;

internal sealed partial class RestExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<RestExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var (status, title, detail, code, retryAfter) = RestExceptionMapping.Map(exception);
        if (status >= StatusCodes.Status500InternalServerError)
            LogUnhandledServerError(logger, exception);
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
        });
        return true;
    }

    [LoggerMessage(EventId = 8000, Level = LogLevel.Error,
        Message = "REST request failed with an unhandled server error.")]
    private static partial void LogUnhandledServerError(ILogger logger, Exception exception);
}
