using BotFramework.Host.Analytics.ClickHouse;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Composition.Migrations;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Host.Contracts.Analytics;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Contracts.ResponsibleGaming;
using BotFramework.Host.Economics.Services;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Modules.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Composition.ServiceDatabases;

public static class ServiceDatabaseBuilderExtensions
{
    public static IHostApplicationBuilder AddOwnedPostgresDatabase(
        this IHostApplicationBuilder builder,
        params IModuleMigrations[] migrations)
    {
        DapperTypeHandlers.Register();
        builder.Services.AddOptions<PostgresConnectionOptions>()
            .Configure(options => options.ConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? string.Empty)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionStrings:Postgres is required.")
            .ValidateOnStart();
        builder.Services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        builder.Services.AddHealthChecks()
            .AddCheck<PostgresDatabaseHealthCheck>(
                "postgres",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready"]);
        foreach (var migration in migrations)
            builder.Services.AddSingleton<IModuleMigrations>(migration);
        builder.Services.AddHostedService<OwnedDatabaseMigrationRunner>();
        return builder;
    }

    public static IHostApplicationBuilder AddWalletServiceDatabase(this IHostApplicationBuilder builder)
    {
        builder.AddOwnedPostgresDatabase(new WalletOwnedMigrations());
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddOptions<BotFrameworkOptions>()
            .Bind(builder.Configuration.GetSection(BotFrameworkOptions.SectionName));
        builder.Services.AddOptions<ClickHouseOptions>()
            .Bind(builder.Configuration.GetSection(ClickHouseOptions.SectionName));

        builder.Services.AddSingleton<ClickHouseAnalyticsService>();
        builder.Services.AddSingleton<IAnalyticsService>(provider => provider.GetRequiredService<ClickHouseAnalyticsService>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ClickHouseAnalyticsService>());

        builder.Services.AddSingleton<RuntimeTuningAccessor>();
        builder.Services.AddSingleton<IRuntimeTuningAccessor>(provider => provider.GetRequiredService<RuntimeTuningAccessor>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<RuntimeTuningAccessor>());

        builder.Services.AddSingleton<IEconomicsService, EconomicsService>();
        builder.Services.AddSingleton<IWalletAtomicExecutionService, WalletAtomicExecutionService>();
        builder.Services.AddSingleton<IWalletReadService, WalletReadService>();
        builder.Services.AddSingleton<IWalletAnalyticsService, WalletAnalyticsService>();
        builder.Services.AddSingleton<IDailyBonusService, DailyBonusService>();
        builder.Services.AddSingleton<IPlayerProtectionService, PlayerProtectionService>();
        return builder;
    }

    private sealed class WalletOwnedMigrations : IModuleMigrations
    {
        public string ModuleId => "wallet";
        public IReadOnlyList<Migration> Migrations { get; } = new FrameworkMigrations().WalletMigrations;
    }
}
