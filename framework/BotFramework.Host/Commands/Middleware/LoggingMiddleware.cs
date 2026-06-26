using System.Diagnostics;
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
                ctx.Command.ModuleId, commandType, ctx.Request.UserId, ctx.Request.TraceId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            LogCommandFailed(log, ex,
                ctx.Command.ModuleId, commandType, ctx.Request.UserId, ctx.Request.TraceId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information,
        Message = "cmd module={ModuleId} type={CommandType} user={UserId} trace={TraceId} ms={Ms} outcome=ok")]
    private static partial void LogCommandSucceeded(
        ILogger logger, string moduleId, string commandType, long userId, string traceId, long ms);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Error,
        Message = "cmd module={ModuleId} type={CommandType} user={UserId} trace={TraceId} ms={Ms} outcome=error")]
    private static partial void LogCommandFailed(
        ILogger logger, Exception exception, string moduleId, string commandType, long userId, string traceId, long ms);
}
