using System.Diagnostics;
using BotFramework.Contracts.Messaging;
namespace BotFramework.Host.Commands.Middleware;

public sealed partial class LoggingMiddleware(ILogger<LoggingMiddleware> log) : ICommandMiddleware
{
    public async Task InvokeAsync(CommandContext ctx, Func<Task> next)
    {
        var sw = Stopwatch.StartNew();
        var commandType = ctx.Command.GetType().Name;

        try
        {
            await next();
            LogCommandSucceeded(log,
                ctx.Command.ModuleId, commandType, UserId(ctx.Request), ctx.Request.CorrelationId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            LogCommandFailed(log, ex,
                ctx.Command.ModuleId, commandType, UserId(ctx.Request), ctx.Request.CorrelationId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private static string UserId(RequestMetadata request) =>
        request.Player?.Value ?? request.UserId ?? "anonymous";

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information,
        Message = "cmd module={ModuleId} type={CommandType} user={UserId} trace={TraceId} ms={Ms} outcome=ok")]
    private static partial void LogCommandSucceeded(
        ILogger logger, string moduleId, string commandType, string userId, string traceId, long ms);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Error,
        Message = "cmd module={ModuleId} type={CommandType} user={UserId} trace={TraceId} ms={Ms} outcome=error")]
    private static partial void LogCommandFailed(
        ILogger logger, Exception exception, string moduleId, string commandType, string userId, string traceId, long ms);
}
