using BotFramework.Host.Composition.Builder;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class FrameworkCapTransportTests
{
    [Fact]
    public void DefaultTransportKeepsRedisCompatibility()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Redis:Enabled"] = "true",
        });

        Assert.Equal(FrameworkCapTransportKind.Redis, FrameworkCapTransport.Resolve(configuration));
    }

    [Fact]
    public void KafkaTransportDoesNotRequireRedis()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Messaging:Transport"] = "Kafka",
            ["Kafka:BootstrapServers"] = "redpanda:9092",
        });

        Assert.Equal(FrameworkCapTransportKind.Kafka, FrameworkCapTransport.Resolve(configuration));
    }

    [Fact]
    public void LocalTransportCanBeSelectedExplicitly()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Messaging:Transport"] = "Local",
            ["Redis:Enabled"] = "true",
        });

        Assert.Equal(FrameworkCapTransportKind.Local, FrameworkCapTransport.Resolve(configuration));
    }

    [Fact]
    public void UnknownTransportFailsFast()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Messaging:Transport"] = "RabbitMQ",
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => FrameworkCapTransport.Resolve(configuration));

        Assert.Contains("Messaging:Transport", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
