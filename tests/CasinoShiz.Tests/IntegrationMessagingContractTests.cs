using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Host.Messaging;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class IntegrationMessagingContractTests
{
    [Fact]
    public void RouterUsesStableTopicAndTenantAggregateKey()
    {
        var router = new DefaultIntegrationMessageRouter();

        var route = router.Route(
            IntegrationMessageKind.Event,
            "test.event.v1",
            new RoutedTestEvent("test.event.v1", DateTimeOffset.UtcNow, "aggregate-7"),
            "tenant-a",
            "scope-a",
            "player-1");

        Assert.Equal(IntegrationMessagingTopics.Events, route.Topic);
        Assert.Equal("tenant-a:scope-a:aggregate-7:player-1", route.MessageKey);
    }

    [Fact]
    public void RouterAllowsContractToOverrideTopicAndKey()
    {
        var route = new DefaultIntegrationMessageRouter().Route(
            IntegrationMessageKind.Command,
            "test.command.v1",
            new RoutedTestCommand("test.command.v1", DateTimeOffset.UtcNow, "custom.topic", "custom-key"),
            "tenant-a",
            "scope-a",
            null);

        Assert.Equal("custom.topic", route.Topic);
        Assert.Equal("custom-key", route.MessageKey);
    }

    [Fact]
    public void SchemaValidatorRejectsUnsupportedVersion()
    {
        var envelope = new IntegrationEventEnvelope(
            "message-1",
            "test.event.v1",
            StableName<RoutedTestEvent>(),
            2,
            "{}",
            DateTimeOffset.UtcNow,
            "correlation-1",
            "causation-1",
            "tenant-a",
            "scope-a",
            null,
            BotChannel.Rest);

        var exception = Assert.Throws<IntegrationSchemaValidationException>(
            () => new IntegrationMessageSchemaValidator().DeserializeEvent(envelope));

        Assert.Equal("unsupported_schema_version", exception.Code);
    }

    [Fact]
    public void SchemaValidatorDeserializesAndChecksContractType()
    {
        var message = new RoutedTestEvent("test.event.v1", DateTimeOffset.UtcNow, "aggregate-7");
        var envelope = new IntegrationEventEnvelope(
            "message-1",
            message.EventType,
            StableName<RoutedTestEvent>(),
            1,
            JsonSerializer.Serialize(message),
            message.OccurredAt,
            "correlation-1",
            "causation-1",
            "tenant-a",
            "scope-a",
            null,
            BotChannel.Rest);

        var parsed = new IntegrationMessageSchemaValidator().DeserializeEvent(envelope);

        Assert.IsType<RoutedTestEvent>(parsed.Message);
    }

    private static string StableName<T>() =>
        $"{typeof(T).Assembly.GetName().Name}:{typeof(T).FullName}";

    public sealed record RoutedTestEvent(
        string EventType,
        DateTimeOffset OccurredAt,
        string AggregateId) : IIntegrationEvent;

    public sealed record RoutedTestCommand(
        string CommandType,
        DateTimeOffset OccurredAt,
        string? Topic,
        string? MessageKey) : IIntegrationCommand, IIntegrationMessageRouted;
}
