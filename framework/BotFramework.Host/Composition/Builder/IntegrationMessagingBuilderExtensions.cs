using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Composition.Migrations;
using BotFramework.Host.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BotFramework.Host.Composition.Builder;

public static class IntegrationMessagingBuilderExtensions
{
    public static bool AddFrameworkIntegrationMessaging(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var configuration = builder.Configuration;
        var services = builder.Services;
        var postgres = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required for integration messaging.");
        var transport = FrameworkCapTransport.Resolve(configuration);
        var group = new string(serviceName
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray());
        if (string.IsNullOrWhiteSpace(group))
            group = "service";

        services.TryAddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.TryAddScoped<IIntegrationInboxContextAccessor, IntegrationInboxContextAccessor>();
        services.TryAddScoped<IIntegrationInbox, PostgresIntegrationInbox>();
        services.TryAddSingleton<PostgresIntegrationOutbox>();
        services.TryAddSingleton<IIntegrationOutbox>(sp => sp.GetRequiredService<PostgresIntegrationOutbox>());
        services.TryAddSingleton<IIntegrationOutboxStore>(sp => sp.GetRequiredService<PostgresIntegrationOutbox>());
        services.TryAddSingleton<IIntegrationMessageRouter, DefaultIntegrationMessageRouter>();
        services.TryAddSingleton<IntegrationMessageSchemaValidator>();
        services.TryAddSingleton<IIntegrationMessageQuarantineStore, PostgresIntegrationMessageQuarantineStore>();
        services.TryAddSingleton(new IntegrationInboxOptions(group));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleMigrations, IntegrationInboxMigrations>());
        services.TryAddSingleton<IntegrationEventDispatcher>();
        services.TryAddSingleton<IntegrationCommandDispatcher>();

        if (transport == FrameworkCapTransportKind.Local)
        {
            services.TryAddScoped<IIntegrationEventPublisher, LocalIntegrationEventPublisher>();
            services.TryAddScoped<IIntegrationCommandPublisher, LocalIntegrationCommandPublisher>();
            return false;
        }

        services.AddCap(options =>
        {
            FrameworkCapTransport.Configure(
                options,
                configuration,
                postgres,
                $"casinoshiz.integration.{group}");
        });
        services.AddScoped<IIntegrationEventPublisher, CapIntegrationEventPublisher>();
        services.AddScoped<IIntegrationCommandPublisher, CapIntegrationCommandPublisher>();
        services.AddSingleton<CapIntegrationEventConsumer>();
        services.AddSingleton<CapIntegrationCommandConsumer>();
        services.AddHostedService<IntegrationOutboxDispatcher>();
        return true;
    }
}
