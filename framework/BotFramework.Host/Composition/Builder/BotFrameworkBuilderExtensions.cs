using BotFramework.Host.Contracts.Telegram;
using BotFramework.Host.TelegramOutbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Caching;
using BotFramework.Host.Caching;
using BotFramework.Host.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BotFramework.Scheduling.Quartz;
using BotFramework.Contracts.Games;
using BotFramework.Contracts.Operations;
using BotFramework.Host.Events.Replay;
using BotFramework.Host.Fairness;
using BotFramework.Host.Games;
using BotFramework.Host.Execution;

namespace BotFramework.Host.Composition.Builder;

public static class BotFrameworkBuilderExtensions
{
    public static IBotFrameworkBuilder AddBackendFramework(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        DapperTypeHandlers.Register();

        services.TryAddSingleton(TimeProvider.System);

        services.AddRazorPages();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddAntiforgery();
        services.AddSession(opts =>
        {
            opts.Cookie.HttpOnly = true;
            opts.Cookie.SameSite = SameSiteMode.Lax;
            opts.Cookie.IsEssential = true;
            opts.IdleTimeout = TimeSpan.FromDays(30);
        });
        services.AddSingleton<TelegramLoginVerifier>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
            return new TelegramLoginVerifier(opts.Token);
        });
        services.AddScoped<IAdminAuditLog, AdminAuditLog>();
        services.AddScoped<IAdminAuditReader, AdminAuditReader>();

        services.AddOptions<BotFrameworkOptions>()
            .Bind(configuration.GetSection(BotFrameworkOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Token),
                "Bot:Token is required when Bot:Enabled is true.")
            .Validate(options => !options.IsProduction || Uri.TryCreate(options.WebhookBaseUrl, UriKind.Absolute, out var uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
                "Bot:WebhookBaseUrl must be an absolute HTTPS URL in production.")
            .ValidateOnStart();

        services.AddOptions<PostgresConnectionOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("Postgres") ?? "")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "ConnectionStrings:Postgres is required.")
            .ValidateOnStart();
        var operationsSection = configuration.GetSection(OperationsSecurityOptions.SectionName);
        services.AddOptions<OperationsSecurityOptions>()
            .Bind(operationsSection)
            .Configure(options => options.Required = operationsSection.Exists())
            .Validate(options => !options.Required || !string.IsNullOrWhiteSpace(options.ApiKey),
                "Services:Operations:ApiKey is required when the Operations section is configured.")
            .ValidateOnStart();

        services.AddSingleton<ICommandBus, CommandBus>();
        services.AddScoped<IRequestClient, LocalRequestClient>();
        services.TryAddScoped<IIntegrationEventPublisher, LocalIntegrationEventPublisher>();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Redis:ConnectionString is required when Redis:Enabled is true.")
            .Validate(options => options.PartitionCount > 0 && options.MaxProcessingAttempts > 0,
                "Redis partition and retry counts must be positive.")
            .ValidateOnStart();
        services.AddOptions<TelegramOutboxTransportOptions>()
            .Bind(configuration.GetSection(TelegramOutboxTransportOptions.SectionName))
            .Validate(options => string.Equals(options.Transport, "Local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(options.Transport, "Cap", StringComparison.OrdinalIgnoreCase),
                "TelegramOutbox:Transport must be Local or Cap.")
            .ValidateOnStart();
        var redisEnabled = configuration.GetValue<bool>($"{RedisOptions.SectionName}:Enabled");
        var redisConn = configuration.GetValue<string>($"{RedisOptions.SectionName}:ConnectionString");
        var useCapOutboxTransport = string.Equals(
            configuration[$"{TelegramOutboxTransportOptions.SectionName}:Transport"],
            "Cap",
            StringComparison.OrdinalIgnoreCase);
        if (redisEnabled && string.IsNullOrWhiteSpace(redisConn))
        {
            throw new InvalidOperationException(
                "Redis:Enabled is true but Redis:ConnectionString is not set.");
        }

        if (useCapOutboxTransport && !redisEnabled)
        {
            throw new InvalidOperationException(
                "TelegramOutbox:Transport=Cap requires Redis because CAP transport is enabled.");
        }

        var botIsProduction = configuration.GetValue<bool>($"{BotFrameworkOptions.SectionName}:IsProduction");
        if (botIsProduction && !redisEnabled)
        {
            throw new InvalidOperationException(
                "Production mode requires Redis. Set Redis:Enabled=true and Redis:ConnectionString.");
        }

        var pgConnStr = configuration.GetConnectionString("Postgres")!;
        services.AddSingleton<DomainEventSubscriptionDispatcher>();
        if (redisEnabled)
        {
            services.AddCap(opts =>
            {
                opts.UsePostgreSql(pgConnStr);
                opts.UseRedis(redisConn!);
                opts.DefaultGroupName = "casinoshiz";
            });
            services.AddSingleton<CapEventBus>();
            services.AddSingleton<IDomainEventBus>(sp => sp.GetRequiredService<CapEventBus>());
            services.AddSingleton<CapEventConsumer>();
        }
        else
        {
            services.AddSingleton<IDomainEventBus>(sp => new InProcessEventBus(
                sp.GetRequiredService<DomainEventSubscriptionDispatcher>()));
        }
        services.AddHostedService<EventSubscriptionInitializer>();

        services.AddSingleton<HealthEndpoint>();
        services.AddSingleton<ILocalizer, Localizer>();
        services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IGameExecutionSessionFactory, PostgresGameExecutionSessionFactory>();
        services.AddSingleton<ICommandInbox, PostgresCommandInbox>();
        services.AddSingleton<IAtomicEconomics, PostgresAtomicEconomics>();
        services.AddSingleton<IAtomicQuotaStore, PostgresAtomicQuotaStore>();
        services.AddSingleton<IAtomicGameAvailability, PostgresAtomicGameAvailability>();
        services.AddSingleton<IAtomicPlayerProtection, PostgresAtomicPlayerProtection>();
        services.AddSingleton<ITransactionalEventCollector, TransactionalEventCollector>();
        services.AddSingleton<ITransactionalScheduleCollector, TransactionalScheduleCollector>();
        services.AddSingleton<GameExecutionTelemetry>();
        services.AddScoped<IGameEffectHandler, PostgresWalletEconomyEffectHandler>();
        services.AddSingleton<PostgresGameEventOutbox>();
        services.AddSingleton<PostgresGameScheduleOutbox>();
        services.AddScoped(typeof(IAtomicGameExecutor<,,>), typeof(AtomicGameExecutor<,,>));
        services.AddSingleton<PostgresTelegramOutboxStore>();
        services.AddSingleton<ITelegramOutboxStore>(sp => sp.GetRequiredService<PostgresTelegramOutboxStore>());
        services.AddSingleton<ITelegramOutbox>(sp => sp.GetRequiredService<PostgresTelegramOutboxStore>());
        services.AddSingleton<ITelegramOutboxMonitor>(sp => sp.GetRequiredService<PostgresTelegramOutboxStore>());
        if (useCapOutboxTransport)
        {
            services.AddSingleton<TelegramOutboxCapRelayService>();
            services.AddSingleton<TelegramOutboxCapReceiptConsumer>();
        }

        services.AddSingleton<IEconomicsService, EconomicsService>();
        services.AddSingleton<IWalletReadService, WalletReadService>();
        services.AddSingleton<IWalletAnalyticsService, WalletAnalyticsService>();
        services.AddSingleton<IDistributedGameLock, PostgresDistributedGameLock>();
        services.AddSingleton<IMiniGameSessionStore, PostgresMiniGameSessionStore>();
        services.AddSingleton<IMiniGameRollGateStore, PostgresMiniGameRollGateStore>();
        services.Configure<DailyBonusOptions>(configuration.GetSection(DailyBonusOptions.SectionName));
        services.AddSingleton<IDailyBonusService, DailyBonusService>();
        services.AddSingleton<IPlayerProtectionService, PlayerProtectionService>();
        services.AddScoped<PostgresGameAvailabilityService>();
        services.AddScoped<IGameAvailabilityService>(sp => sp.GetRequiredService<PostgresGameAvailabilityService>());
        services.AddScoped<IGameAvailabilityClient>(sp => sp.GetRequiredService<PostgresGameAvailabilityService>());
        services.AddScoped<GameAvailabilityGrpcInterceptor>();

        services.Configure<TelegramDiceDailyLimitOptions>(
            configuration.GetSection(TelegramDiceDailyLimitOptions.SectionName));
        services.AddSingleton<ITelegramDiceDailyRollLimiter, TelegramDiceDailyRollLimiter>();

        services.AddSingleton<RuntimeTuningAccessor>();
        services.AddSingleton<IRuntimeTuningAccessor>(sp => sp.GetRequiredService<RuntimeTuningAccessor>());

        services.AddOptions<ClickHouseOptions>()
            .Bind(configuration.GetSection(ClickHouseOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host),
                "ClickHouse:Host is required when ClickHouse:Enabled is true.")
            .Validate(options => options.BufferSize > 0 && options.FlushIntervalMs > 0,
                "ClickHouse buffer and flush interval must be positive.")
            .ValidateOnStart();
        services.AddSingleton<ClickHouseAnalyticsService>();
        services.AddSingleton<IAnalyticsService>(sp => sp.GetRequiredService<ClickHouseAnalyticsService>());
        services.AddHostedService(sp => sp.GetRequiredService<ClickHouseAnalyticsService>());
        services.AddSingleton<IAnalyticsQueryService, ClickHouseAnalyticsQueryService>();

        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IEventStore, PostgresEventStore>();
        services.AddScoped<EventDispatcher>();
        services.AddScoped<IEventReplayService, EventReplayService>();
        services.AddScoped<IReadOnlyEventReplayService, ReadOnlyEventReplayService>();
        services.AddSingleton<IEconomySimulationService, EconomySimulationService>();
        services.AddScoped<IRandomOutcomeGenerator, PostgresRandomOutcomeGenerator>();
        services.AddScoped<IEventDispatchRetryService, EventDispatchRetryService>();
        services.AddSingleton<IEventDispatchFailureStore, PostgresEventDispatchFailureStore>();
        services.AddSingleton(typeof(ISnapshotStore<>), typeof(PostgresSnapshotStore<>));
        services.AddSingleton<IEventLog, PostgresEventLog>();
        services.AddSingleton<EventLogSubscriber>();
        services.AddSingleton<ClickHouseEventMirror>();
        services.AddSingleton<IBackgroundJobStatusService, BackgroundJobStatusService>();
        services.AddScoped<BotFramework.Contracts.Operations.IOperationsAdminService, BotFramework.Host.Admin.Operations.OperationsAdminService>();

        services.AddHostedService<ModuleMigrationRunner>();
        if (useCapOutboxTransport)
            services.AddHostedService(sp => sp.GetRequiredService<TelegramOutboxCapRelayService>());
        services.AddQuartzGameScheduling(pgConnStr);
        services.AddHostedService<EventAnalyticsBackfillService>();
        services.AddHostedService<GameEventOutboxDispatcher>();
        services.AddHostedService<GameScheduleOutboxDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<RuntimeTuningAccessor>());
        services.AddHostedService<DailyBonusCatchUpHostedService>();

        services.AddHostedService<BackgroundJobRunner>();
        services.AddQuartzRecurringCommandBootstrapper();

        if (redisEnabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn!));
            services.AddSingleton<ICacheStore, RedisCacheStore>();
        }

        return new BotFrameworkBuilder(services, configuration);
    }
}
