using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class GameExecutionTelemetryTests
{
    [Fact]
    public void AcceptedExecution_EmitsFixedLinearStages()
    {
        Activity? completed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GameExecutionTelemetry.InstrumentationName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => completed = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var telemetry = CreateTelemetry();

        using (var observation = telemetry.Start("dice", "command-1", "aggregate-1"))
        {
            observation.LockWaitStarted();
            observation.Locked(TimeSpan.FromMilliseconds(3));
            observation.Decided(DecisionStatus.Accepted, null);
            observation.Committing();
            observation.Committed();
            observation.TransactionFinished(TimeSpan.FromMilliseconds(12));
        }

        Assert.NotNull(completed);
        Assert.Equal("game.command.execute", completed.OperationName);
        Assert.Equal(
            [
                "game.command.received",
                "game.command.lock_wait",
                "game.command.locked",
                "game.command.decided",
                "game.command.committing",
                "game.command.committed",
            ],
            completed.Events.Select(stage => stage.Name));
        Assert.Equal("dice", completed.GetTagItem("game.id"));
        Assert.Equal("command-1", completed.GetTagItem("game.command.id"));
        Assert.Equal("aggregate-1", completed.GetTagItem("game.aggregate.id"));
        Assert.Equal(ActivityStatusCode.Ok, completed.Status);
    }

    [Fact]
    public void Metrics_CoverDurationsDuplicatesRejectionsRollbacksLagAndConcurrency()
    {
        var measurements = new ConcurrentBag<Measurement>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == GameExecutionTelemetry.InstrumentationName)
                    meterListener.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, CopyTags(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, CopyTags(tags))));
        listener.Start();
        var telemetry = CreateTelemetry();

        using (var duplicate = telemetry.Start("dice", "duplicate", "aggregate"))
        {
            duplicate.LockWaitStarted();
            duplicate.Locked(TimeSpan.FromMilliseconds(2));
            duplicate.Duplicate();
            duplicate.Committing();
            duplicate.Committed();
            duplicate.TransactionFinished(TimeSpan.FromMilliseconds(4));
        }
        using (var rejected = telemetry.Start("dice", "rejected", "aggregate"))
        {
            rejected.Decided(DecisionStatus.Rejected, "insufficient_balance");
            rejected.Committing();
            rejected.Committed();
            rejected.TransactionFinished(TimeSpan.FromMilliseconds(5));
        }
        using (var failed = telemetry.Start("dice", "failed", "aggregate"))
        {
            failed.Failed(new InvalidOperationException("injected"));
            failed.RolledBack();
            failed.TransactionFinished(TimeSpan.FromMilliseconds(6));
        }
        GameExecutionTelemetry.RecordOutboxLag(
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(7));

        var names = measurements.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("game.command.received", names);
        Assert.Contains("game.command.duplicates", names);
        Assert.Contains("game.command.rejections", names);
        Assert.Contains("game.command.failures", names);
        Assert.Contains("game.command.rollbacks", names);
        Assert.Contains("game.command.active", names);
        Assert.Contains("game.command.lock_wait.duration", names);
        Assert.Contains("game.command.execution.duration", names);
        Assert.Contains("game.command.transaction.duration", names);
        Assert.Contains("game.outbox.delivery.lag", names);

        var rejection = Assert.Single(measurements.Where(item => item.Name == "game.command.rejections"));
        Assert.Equal("insufficient_balance", rejection.Tags["reason"]);
        Assert.Contains(measurements, item => item.Name == "game.outbox.delivery.lag" && item.Value == 7);
        Assert.DoesNotContain(measurements, item =>
            item.Tags.ContainsKey("game.command.id") || item.Tags.ContainsKey("game.aggregate.id"));
    }

    private static GameExecutionTelemetry CreateTelemetry() =>
        new(NullLogger<GameExecutionTelemetry>.Instance);

    private static Dictionary<string, object?> CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags) result[tag.Key] = tag.Value;
        return result;
    }

    private sealed record Measurement(string Name, double Value, IReadOnlyDictionary<string, object?> Tags);
}
