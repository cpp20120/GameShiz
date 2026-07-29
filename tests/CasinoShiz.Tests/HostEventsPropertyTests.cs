using BotFramework.Host.Analytics;
using BotFramework.Host.Events.Bus;
using BotFramework.Host.Events.Dispatch;
using BotFramework.Sdk.Events.Bus;
using BotFramework.Sdk.Events.Contracts;
using BotFramework.Sdk.Projections;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class HostEventsPropertyTests
{
    [Property(MaxTest = 100)]
    public async Task<Property> SubscriptionDispatcher_MatchesPatternsAndIsolatesFailures(
        NonEmptyArray<int> rawPatterns,
        NonNegativeInt rawEvent)
    {
        var eventType = EventType(rawEvent.Get);
        var dispatcher = new DomainEventSubscriptionDispatcher(NullLogger<DomainEventSubscriptionDispatcher>.Instance);
        var calls = new List<int>();
        var expected = new List<int>();
        var patterns = rawPatterns.Get
            .Select((value, index) => (Pattern: PatternFor(value, eventType), Index: index))
            .ToArray();

        foreach (var (pattern, index) in patterns)
        {
            var subscriber = index % 5 == 0
                ? new RecordingSubscriber(calls, index, fail: true)
                : new RecordingSubscriber(calls, index, fail: false);
            dispatcher.Subscribe(pattern, subscriber);
            if (Matches(pattern, eventType) && index % 5 != 0)
                expected.Add(index);
        }

        await dispatcher.DispatchAsync(new TestEvent(eventType, rawEvent.Get), CancellationToken.None);

        return calls.SequenceEqual(expected)
            .ToProperty()
            .Label($"event={eventType}, subscriptions={patterns.Length}, delivered={calls.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> EventDispatcher_AppliesProjectionsBeforeBusAndRestoresEnvelope(
        NonNegativeInt rawEvent,
        NonNegativeInt rawProjectionCount,
        NonNegativeInt rawCorrelation)
    {
        var eventType = EventType(rawEvent.Get);
        var trace = new List<string>();
        var projections = Enumerable.Range(0, rawProjectionCount.Get % 8)
            .Select(index => (IProjection)new RecordingProjection(index, eventType, trace))
            .ToArray();
        var bus = new RecordingBus(trace);
        var dispatcher = new EventDispatcher(projections, bus);
        var previous = new EventAnalyticsEnvelope("previous", 7, "previous.aggregate", 9, "old-correlation", "old-causation");
        EventAnalyticsEnvelopeAccessor.Current = previous;
        AnalyticsContextAccessor.Current = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["correlation_id"] = $"correlation-{rawCorrelation.Get}",
            ["causation_id"] = $"causation-{rawCorrelation.Get}",
        };

        try
        {
            await dispatcher.DispatchAsync("stream-1", 3, new TestEvent(eventType, rawEvent.Get), null, CancellationToken.None);
            var expectedTrace = Enumerable.Range(0, projections.Length)
                .Select(index => $"projection:{index}")
                .Append("bus");
            var envelope = Assert.IsType<EventAnalyticsEnvelope>(bus.EnvelopeSeen);
            var valid = trace.SequenceEqual(expectedTrace)
                && envelope.StreamId == "stream-1"
                && envelope.StreamVersion == 3
                && envelope.CorrelationId == $"correlation-{rawCorrelation.Get}"
                && envelope.CausationId == $"causation-{rawCorrelation.Get}"
                && EventAnalyticsEnvelopeAccessor.Current == previous;

            return valid
                .ToProperty()
                .Label($"event={eventType}, projections={projections.Length}, trace={string.Join(',', trace)}");
        }
        finally
        {
            AnalyticsContextAccessor.Current = null;
            EventAnalyticsEnvelopeAccessor.Current = null;
        }
    }

    [Property(MaxTest = 100)]
    public async Task<Property> EventDispatcher_RestoresEnvelopeWhenBusFails(NonNegativeInt raw)
    {
        var previous = new EventAnalyticsEnvelope("before", 1, "aggregate", 1, "correlation", "causation");
        EventAnalyticsEnvelopeAccessor.Current = previous;
        AnalyticsContextAccessor.Current = null;
        var dispatcher = new EventDispatcher([], new FailingBus());

        var failed = false;
        var restored = false;
        try
        {
            await dispatcher.DispatchAsync("stream", raw.Get, new TestEvent(EventType(raw.Get), raw.Get), null, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            failed = true;
        }
        finally
        {
            restored = EventAnalyticsEnvelopeAccessor.Current == previous;
            EventAnalyticsEnvelopeAccessor.Current = null;
        }

        return (failed && restored)
            .ToProperty()
            .Label($"event={raw.Get}, failed={failed}");
    }

    private static string EventType(int value) => $"module{(uint)value % 4}.action{(uint)value / 4 % 5}";

    private static string PatternFor(int value, string eventType)
    {
        var module = eventType[..eventType.IndexOf('.')];
        var action = eventType[(eventType.IndexOf('.') + 1)..];
        return ((uint)value % 6) switch
        {
            0 => eventType,
            1 => "*",
            2 => $"{module}.*",
            3 => $"*.{action}",
            4 => $"other{(uint)value % 3}.*",
            _ => "nodot",
        };
    }

    private static bool Matches(string pattern, string eventType)
    {
        if (pattern == "*" || pattern == eventType) return true;
        var patternDot = pattern.IndexOf('.');
        var eventDot = eventType.IndexOf('.');
        if (patternDot < 0 || eventDot < 0) return false;
        var patternModule = pattern[..patternDot];
        var patternAction = pattern[(patternDot + 1)..];
        var eventModule = eventType[..eventDot];
        var eventAction = eventType[(eventDot + 1)..];
        return (patternModule == "*" || patternModule == eventModule)
            && (patternAction == "*" || patternAction == eventAction);
    }

    private sealed record TestEvent(string EventType, long OccurredAt) : IDomainEvent;

    private sealed class RecordingSubscriber(List<int> calls, int index, bool fail) : IDomainEventSubscriber
    {
        public Task HandleAsync(IDomainEvent ev, CancellationToken ct)
        {
            if (fail) throw new InvalidOperationException("property failure");
            calls.Add(index);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProjection(int index, string eventType, List<string> trace) : IProjection
    {
        public IReadOnlySet<string> SubscribedEventTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { eventType };

        public Task ApplyAsync(IDomainEvent ev, ProjectionContext ctx, CancellationToken ct)
        {
            trace.Add($"projection:{index}");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBus(List<string> trace) : IDomainEventBus
    {
        public object? EnvelopeSeen { get; private set; }

        public Task PublishAsync(IDomainEvent ev, CancellationToken ct)
        {
            EnvelopeSeen = EventAnalyticsEnvelopeAccessor.Current;
            trace.Add("bus");
            return Task.CompletedTask;
        }

        public void Subscribe(string eventTypePattern, IDomainEventSubscriber subscriber) { }
    }

    private sealed class FailingBus : IDomainEventBus
    {
        public Task PublishAsync(IDomainEvent ev, CancellationToken ct) =>
            throw new InvalidOperationException("property failure");

        public void Subscribe(string eventTypePattern, IDomainEventSubscriber subscriber) { }
    }
}
