using System.Net;

namespace BotFramework.Client;

public sealed class BotFrameworkApiException : HttpRequestException
{
    public BotFrameworkApiException()
        : this(new(null, null, null, null, null, null, null, null), HttpStatusCode.BadRequest)
    {
    }

    public BotFrameworkApiException(string message)
        : base(message)
    {
        Problem = new(null, message, null, message, null, null, null, null);
    }

    public BotFrameworkApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        Problem = new(null, message, null, message, null, null, null, null);
    }

    public BotFrameworkApiException(BotFrameworkProblemDetails problem, HttpStatusCode statusCode)
        : base(problem.Detail ?? problem.Title, null, statusCode)
    {
        Problem = problem;
    }

    public BotFrameworkProblemDetails Problem { get; }
}
