namespace BotFramework.Host.Runtime;

public interface IBackgroundJobStatusService
{
    IReadOnlyList<BackgroundJobStatusSnapshot> Snapshot();
    void Register(string jobName, string kind = "module");
    void MarkStarting(string jobName);
    void MarkRunning(string jobName);
    void MarkCompleted(string jobName);
    void MarkCrashed(string jobName, Exception exception, int backoffMs);
    void MarkFailed(string jobName, Exception exception);
    void MarkWaiting(string jobName, DateTimeOffset nextRunAt, string? note = null);
    void MarkStopped(string jobName);
}
