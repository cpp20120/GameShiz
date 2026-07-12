using System.Diagnostics;
using System.Diagnostics.Metrics;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed partial class GameExecutionTelemetry(ILogger<GameExecutionTelemetry> logger)
{
    public const string InstrumentationName = "CasinoShiz.GameExecution";

    private static readonly ActivitySource Activities = new(InstrumentationName);
    private static readonly Meter Metrics = new(InstrumentationName);
    private static readonly Counter<long> Received = Metrics.CreateCounter<long>("game.command.received");
    private static readonly Counter<long> Duplicates = Metrics.CreateCounter<long>("game.command.duplicates");
    private static readonly Counter<long> Rejections = Metrics.CreateCounter<long>("game.command.rejections");
    private static readonly Counter<long> Failures = Metrics.CreateCounter<long>("game.command.failures");
    private static readonly Counter<long> Rollbacks = Metrics.CreateCounter<long>("game.command.rollbacks");
    private static readonly UpDownCounter<long> Active = Metrics.CreateUpDownCounter<long>("game.command.active");
    private static readonly Histogram<double> LockWait = Metrics.CreateHistogram<double>(
        "game.command.lock_wait.duration", "s");
    private static readonly Histogram<double> ExecutionDuration = Metrics.CreateHistogram<double>(
        "game.command.execution.duration", "s");
    private static readonly Histogram<double> TransactionDuration = Metrics.CreateHistogram<double>(
        "game.command.transaction.duration", "s");
    private static readonly Histogram<double> OutboxLag = Metrics.CreateHistogram<double>(
        "game.outbox.delivery.lag", "s");
    private readonly ILogger logger = logger;

    public Observation Start(string gameId, string commandId, string aggregateId)
    {
        var activity = Activities.StartActivity("game.command.execute", ActivityKind.Internal);
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("game.command.id", commandId);
        activity?.SetTag("game.aggregate.id", aggregateId);

        Received.Add(1, new KeyValuePair<string, object?>("game.id", gameId));
        Active.Add(1, new KeyValuePair<string, object?>("game.id", gameId));
        LogReceived(logger, gameId, commandId, aggregateId);
        AddStage(activity, "game.command.received");
        return new Observation(this, gameId, commandId, activity);
    }

    public static void RecordOutboxLag(DateTimeOffset createdAt, DateTimeOffset deliveredAt)
    {
        var lag = Math.Max(0, (deliveredAt - createdAt).TotalSeconds);
        OutboxLag.Record(lag);
    }

    private static void AddStage(Activity? activity, string name, ActivityTagsCollection? tags = null) =>
        activity?.AddEvent(new ActivityEvent(name, tags: tags));

    internal sealed class Observation : IDisposable
    {
        private readonly GameExecutionTelemetry owner;
        private readonly string gameId;
        private readonly string commandId;
        private readonly Activity? activity;
        private readonly long startedAt = Stopwatch.GetTimestamp();
        private string outcome = "failed";
        private bool disposed;

        public Observation(
            GameExecutionTelemetry owner,
            string gameId,
            string commandId,
            Activity? activity)
        {
            this.owner = owner;
            this.gameId = gameId;
            this.commandId = commandId;
            this.activity = activity;
        }

        public void LockWaitStarted() =>
            Stage("game.command.lock_wait", () => LogLockWait(owner.logger, gameId, commandId));

        public void Locked(TimeSpan elapsed)
        {
            LockWait.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("game.id", gameId));
            Stage("game.command.locked", () => LogLocked(owner.logger, gameId, commandId, elapsed.TotalMilliseconds));
        }

        public void Duplicate()
        {
            outcome = "duplicate";
            Duplicates.Add(1, new KeyValuePair<string, object?>("game.id", gameId));
            Stage("game.command.duplicate", () => LogDuplicate(owner.logger, gameId, commandId));
        }

        public void Decided(DecisionStatus status, string? rejectionReason)
        {
            outcome = status == DecisionStatus.Accepted ? "accepted" : "rejected";
            activity?.SetTag("game.decision.status", outcome);
            Stage("game.command.decided", () => LogDecided(owner.logger, gameId, commandId, outcome));
            if (status != DecisionStatus.Rejected) return;

            var reason = string.IsNullOrWhiteSpace(rejectionReason) ? "unspecified" : rejectionReason;
            Rejections.Add(1,
                new KeyValuePair<string, object?>("game.id", gameId),
                new KeyValuePair<string, object?>("reason", reason));
            activity?.SetTag("game.rejection.reason", reason);
            Stage("game.command.rejected", () => LogRejected(owner.logger, gameId, commandId, reason));
        }

        public void Committing() =>
            Stage("game.command.committing", () => LogCommitting(owner.logger, gameId, commandId));

        public void Committed()
        {
            Stage("game.command.committed", () => LogCommitted(owner.logger, gameId, commandId));
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        public void Failed(Exception exception)
        {
            outcome = "failed";
            Failures.Add(1,
                new KeyValuePair<string, object?>("game.id", gameId),
                new KeyValuePair<string, object?>("exception.type", exception.GetType().Name));
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag("error.type", exception.GetType().FullName);
            Stage("game.command.failed", () => LogFailed(owner.logger, exception, gameId, commandId));
        }

        public void RolledBack() =>
            Rollbacks.Add(1, new KeyValuePair<string, object?>("game.id", gameId));

        public void TransactionFinished(TimeSpan elapsed) =>
            TransactionDuration.Record(
                elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("game.id", gameId),
                new KeyValuePair<string, object?>("outcome", outcome));

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ExecutionDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                new KeyValuePair<string, object?>("game.id", gameId),
                new KeyValuePair<string, object?>("outcome", outcome));
            Active.Add(-1, new KeyValuePair<string, object?>("game.id", gameId));
            activity?.Dispose();
        }

        private void Stage(string name, Action log)
        {
            log();
            AddStage(activity, name);
        }
    }

    [LoggerMessage(1800, LogLevel.Debug, "game.command.received game_id={GameId} command_id={CommandId} aggregate_id={AggregateId}")]
    private static partial void LogReceived(ILogger logger, string gameId, string commandId, string aggregateId);

    [LoggerMessage(1801, LogLevel.Debug, "game.command.lock_wait game_id={GameId} command_id={CommandId}")]
    private static partial void LogLockWait(ILogger logger, string gameId, string commandId);

    [LoggerMessage(1802, LogLevel.Debug, "game.command.locked game_id={GameId} command_id={CommandId} wait_ms={WaitMs}")]
    private static partial void LogLocked(ILogger logger, string gameId, string commandId, double waitMs);

    [LoggerMessage(1803, LogLevel.Information, "game.command.duplicate game_id={GameId} command_id={CommandId}")]
    private static partial void LogDuplicate(ILogger logger, string gameId, string commandId);

    [LoggerMessage(1804, LogLevel.Debug, "game.command.decided game_id={GameId} command_id={CommandId} outcome={Outcome}")]
    private static partial void LogDecided(ILogger logger, string gameId, string commandId, string outcome);

    [LoggerMessage(1805, LogLevel.Debug, "game.command.committing game_id={GameId} command_id={CommandId}")]
    private static partial void LogCommitting(ILogger logger, string gameId, string commandId);

    [LoggerMessage(1806, LogLevel.Information, "game.command.committed game_id={GameId} command_id={CommandId}")]
    private static partial void LogCommitted(ILogger logger, string gameId, string commandId);

    [LoggerMessage(1807, LogLevel.Information, "game.command.rejected game_id={GameId} command_id={CommandId} reason={Reason}")]
    private static partial void LogRejected(ILogger logger, string gameId, string commandId, string reason);

    [LoggerMessage(1808, LogLevel.Error, "game.command.failed game_id={GameId} command_id={CommandId}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string gameId, string commandId);
}
