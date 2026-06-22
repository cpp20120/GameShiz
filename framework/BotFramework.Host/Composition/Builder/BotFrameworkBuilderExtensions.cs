using BotFramework.Host.Commands;
using BotFramework.Host.Pipeline;
using BotFramework.Host.Redis;
using BotFramework.Sdk;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Telegram.Bot;

namespace BotFramework.Host.Composition.Builder;

public static class BotFrameworkBuilderExtensions
{
    public static IBotFrameworkBuilder AddBotFramework(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        DapperTypeHandlers.Register();

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

        services.Configure<BotFrameworkOptions>(configuration.GetSection(BotFrameworkOptions.SectionName));

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.Token))
                throw new InvalidOperationException(
                    "BotFrameworkOptions.Token is not configured. Set Bot:Token in configuration.");
            return new TelegramBotClient(opts.Token);
        });

        services.AddSingleton<UpdateRouter>();
        services.AddSingleton<UpdatePipeline>();

        services.AddSingleton<IUpdateMiddleware, ExceptionMiddleware>();
        services.AddSingleton<IUpdateMiddleware, UpdateDeduplicationMiddleware>();
        services.AddSingleton<IUpdateMiddleware, UpdateAnalyticsMiddleware>();
        services.AddSingleton<IUpdateMiddleware, BotFramework.Host.Pipeline.Middleware.LoggingMiddleware>();
        services.AddSingleton<IUpdateMiddleware, BotFramework.Host.Pipeline.Middleware.RateLimitMiddleware>();
        services.AddSingleton<IUpdateMiddleware, KnownChatsMiddleware>();

        services.AddSingleton<ICommandBus, CommandBus>();

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        var redisEnabled = configuration.GetValue<bool>($"{RedisOptions.SectionName}:Enabled");
        var redisConn = configuration.GetValue<string>($"{RedisOptions.SectionName}:ConnectionString");
        if (redisEnabled && string.IsNullOrWhiteSpace(redisConn))
            throw new InvalidOperationException(
                "Redis:Enabled is true but Redis:ConnectionString is not set.");
        var botIsProduction = configuration.GetValue<bool>($"{BotFrameworkOptions.SectionName}:IsProduction");
        if (botIsProduction && !redisEnabled)
            throw new InvalidOperationException(
                "Production mode requires Redis. Set Redis:Enabled=true and Redis:ConnectionString.");

        var pgConnStr = configuration.GetConnectionString("Postgres")!;
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
            services.AddSingleton<IDomainEventBus, InProcessEventBus>();
        }
        services.AddHostedService<EventSubscriptionInitializer>();

        services.AddSingleton<HealthEndpoint>();
        services.AddSingleton<ILocalizer, Localizer>();
        services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();

        services.AddSingleton<IEconomicsService, EconomicsService>();
        services.AddSingleton<IDistributedGameLock, PostgresDistributedGameLock>();
        services.AddSingleton<IMiniGameSessionStore, PostgresMiniGameSessionStore>();
        services.AddSingleton<IMiniGameRollGateStore, PostgresMiniGameRollGateStore>();
        services.Configure<DailyBonusOptions>(configuration.GetSection(DailyBonusOptions.SectionName));
        services.AddSingleton<IDailyBonusService, DailyBonusService>();

        services.Configure<TelegramDiceDailyLimitOptions>(
            configuration.GetSection(TelegramDiceDailyLimitOptions.SectionName));
        services.AddSingleton<ITelegramDiceDailyRollLimiter, TelegramDiceDailyRollLimiter>();

        services.AddSingleton<RuntimeTuningAccessor>();
        services.AddSingleton<IRuntimeTuningAccessor>(sp => sp.GetRequiredService<RuntimeTuningAccessor>());

        services.Configure<ClickHouseOptions>(configuration.GetSection(ClickHouseOptions.SectionName));
        services.AddSingleton<ClickHouseAnalyticsService>();
        services.AddSingleton<IAnalyticsService>(sp => sp.GetRequiredService<ClickHouseAnalyticsService>());
        services.AddHostedService(sp => sp.GetRequiredService<ClickHouseAnalyticsService>());
        services.AddSingleton<IAnalyticsQueryService, ClickHouseAnalyticsQueryService>();

        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IEventStore, PostgresEventStore>();
        services.AddScoped<EventDispatcher>();
        services.AddScoped<IEventReplayService, EventReplayService>();
        services.AddScoped<IEventDispatchRetryService, EventDispatchRetryService>();
        services.AddSingleton<IEventDispatchFailureStore, PostgresEventDispatchFailureStore>();
        services.AddSingleton(typeof(ISnapshotStore<>), typeof(PostgresSnapshotStore<>));
        services.AddSingleton<IEventLog, PostgresEventLog>();
        services.AddSingleton<EventLogSubscriber>();
        services.AddSingleton<ClickHouseEventMirror>();
        services.AddSingleton<IBackgroundJobStatusService, BackgroundJobStatusService>();

        services.AddHostedService<ModuleMigrationRunner>();
        services.AddHostedService(sp => sp.GetRequiredService<RuntimeTuningAccessor>());
        services.AddHostedService<DailyBonusCatchUpHostedService>();

        services.AddHostedService<BackgroundJobRunner>();

        services.AddHostedService<BotHostedService>();

        if (redisEnabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn!));
            services.AddSingleton<UpdateStreamPublisher>();
            services.AddHostedService<UpdateStreamWorkerService>();
        }

        return new BotFrameworkBuilder(services, configuration);
    }
}
