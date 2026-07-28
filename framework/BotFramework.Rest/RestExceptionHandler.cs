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
        });
        return true;
    }
}
