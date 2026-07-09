using System.Text.Json;
using BotFramework.Host.Analytics;
using DotNetCore.CAP;

namespace BotFramework.Host.Events.Bus;

public sealed partial class CapEventBus(
    IServiceScopeFactory scopeFactory,
    DomainEventSubscriptionDispatcher dispatcher,
    ILogger<CapEventBus> logger) : IDomainEventBus
{
    internal const string Topic = "domain.event";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    public void Subscribe(string eventTypePattern, IDomainEventSubscriber subscriber) =>
        dispatcher.Subscribe(eventTypePattern, subscriber);

    public async Task PublishAsync(IDomainEvent ev, CancellationToken ct)
    {
        var metadata = EventAnalyticsEnvelopeAccessor.Current;
        var envelope = new DomainEventEnvelope(
            ev.EventType,
            ev.GetType().AssemblyQualifiedName!,
            JsonSerializer.Serialize(ev, ev.GetType(), JsonOpts),
            ev.OccurredAt,
            metadata?.StreamId ?? "",
            metadata?.StreamVersion ?? 0,
            metadata?.AggregateType ?? "",
            metadata?.SchemaVersion ?? 1,
            metadata?.CorrelationId ?? "",
            metadata?.CausationId ?? "");

        using var scope = scopeFactory.CreateScope();
        var cap = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
        await cap.PublishAsync(Topic, envelope, cancellationToken: ct);
    }

    internal async Task DispatchAsync(DomainEventEnvelope envelope, CancellationToken ct)
    {
        var type = Type.GetType(envelope.TypeName);
        if (type is null)
        {
            LogUnknownType(envelope.TypeName);
            return;
        }

        var ev = (IDomainEvent)JsonSerializer.Deserialize(envelope.Payload, type, JsonOpts)!;

        var previousEnvelope = EventAnalyticsEnvelopeAccessor.Current;
        EventAnalyticsEnvelopeAccessor.Current = string.IsNullOrEmpty(envelope.StreamId)
            ? null
            : new EventAnalyticsEnvelope(
                envelope.StreamId,
                envelope.StreamVersion,
                envelope.AggregateType,
                envelope.SchemaVersion,
                envelope.CorrelationId,
                envelope.CausationId);
        try
        {
            await dispatcher.DispatchAsync(ev, ct);
        }
        finally
        {
            EventAnalyticsEnvelopeAccessor.Current = previousEnvelope;
        }
    }

    [LoggerMessage(LogLevel.Warning, "event_bus.unknown_type typeName={TypeName}")]
    partial void LogUnknownType(string typeName);

}
