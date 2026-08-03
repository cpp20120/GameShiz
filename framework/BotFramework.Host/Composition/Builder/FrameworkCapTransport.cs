using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;

namespace BotFramework.Host.Composition.Builder;

/// <summary>
/// Selects the CAP transport in one framework-owned composition point.
/// Domain modules and service hosts do not need to know which broker is used.
/// </summary>
public static class FrameworkCapTransport
{
    public static FrameworkCapTransportKind Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration["Messaging:Transport"]?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
            return configuration.GetValue<bool>("Redis:Enabled")
                ? FrameworkCapTransportKind.Redis
                : FrameworkCapTransportKind.Local;

        return configured.ToLowerInvariant() switch
        {
            "local" or "in-process" or "inprocess" => FrameworkCapTransportKind.Local,
            "redis" or "redis-streams" or "redisstreams" => FrameworkCapTransportKind.Redis,
            "kafka" or "redpanda" => FrameworkCapTransportKind.Kafka,
            _ => throw new InvalidOperationException(
                $"Messaging:Transport '{configured}' is not supported. Use Local, Redis or Kafka."),
        };
    }

    public static void Configure(
        CapOptions options,
        IConfiguration configuration,
        string postgres,
        string consumerGroup)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(postgres);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);

        options.UsePostgreSql(postgres);
        switch (Resolve(configuration))
        {
            case FrameworkCapTransportKind.Redis:
                options.UseRedis(GetRequired(configuration, "Redis:ConnectionString"));
                break;
            case FrameworkCapTransportKind.Kafka:
                options.UseKafka(kafka =>
                {
                    kafka.Servers = GetKafkaServers(configuration);
                    foreach (var setting in configuration.GetSection("Kafka:MainConfig").GetChildren()
                        .Where(setting => !string.IsNullOrWhiteSpace(setting.Key) && setting.Value is not null))
                    {
                        kafka.MainConfig[setting.Key] = setting.Value!;
                    }
                });
                break;
            case FrameworkCapTransportKind.Local:
                throw new InvalidOperationException(
                    "CAP transport was requested while Messaging:Transport is Local.");
        }

        options.DefaultGroupName = consumerGroup;
    }

    private static string GetKafkaServers(IConfiguration configuration)
    {
        var value = configuration["Kafka:Servers"] ?? configuration["Kafka:BootstrapServers"];
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        throw new InvalidOperationException(
            "Kafka:Servers or Kafka:BootstrapServers is required for the selected CAP transport.");
    }

    private static string GetRequired(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"{key} is required for the selected CAP transport.");
}
