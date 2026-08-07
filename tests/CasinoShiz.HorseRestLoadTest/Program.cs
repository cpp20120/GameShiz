using System.Net;
using System.Globalization;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Composition.Builder;
using BotFramework.Rest;
using CasinoShiz.HorseRestLoadTest;
using Games.Horse.Infrastructure.Modules;
using Games.Horse.Rest;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

const string testToken = "horse-load-test";
var port = ReadPort();
var database = new PostgreSqlBuilder("postgres:17-alpine")
    .WithDatabase("casinoshiz_horse_load_test")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();
WebApplication? app = null;

try
{
    await database.StartAsync();

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ApplicationName = typeof(Program).Assembly.GetName().Name,
        EnvironmentName = Environments.Production,
    });

    builder.WebHost.ConfigureKestrel(options =>
        options.Listen(IPAddress.Loopback, port, listen => listen.Protocols = HttpProtocols.Http1));
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["ConnectionStrings:Postgres"] = database.GetConnectionString(),
        ["Bot:Enabled"] = "false",
        ["Redis:Enabled"] = "false",
        ["ClickHouse:Enabled"] = "false",
        ["Rendering:Minio:Enabled"] = "false",
        ["TelegramOutbox:Transport"] = "Local",
        ["RateLimit:Enabled"] = "false",
        ["Rest:ApiVersion"] = "v1",
        ["Rest:OpenApiEnabled"] = "false",
        ["Rest:RequireTenantClaim"] = "true",
        ["Rest:RequireScopeClaim"] = "true",
        ["DurableWorkflow:Mode"] = "Solo",
        ["DurableWorkflow:AutoCreate"] = "true",
        ["Games:horse:AutoRunEnabled"] = "false",
        ["Games:horse:RenderVariants"] = "1",
    });

    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    builder.AddBackendFramework()
        .AddModule<HorseModule>();
    builder.AddRestFramework();
    builder.Services.AddHorseRest();

    // The test host has no production policy store or JWT issuer. The route,
    // REST context and game service are still exercised exactly as in the API.
    builder.Services.AddSingleton<IRateLimitPolicyProvider, DefaultRateLimitPolicyProvider>();
    builder.Services
        .AddAuthentication(LoadTestAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, LoadTestAuthenticationHandler>(
            LoadTestAuthenticationHandler.SchemeName,
            static _ => { });

    if (ReadBoolean("HORSE_LOAD_TEST_SKIP_TENANT_PROVISIONING"))
        builder.Services.AddSingleton<ITenantContextProvisioner, NoOpTenantContextProvisioner>();

    app = builder.Build();
    app.UseRestFramework();
    app.MapRestFramework();
    await app.StartAsync();

    Console.WriteLine($"HORSE_LOAD_TEST_READY http://127.0.0.1:{port}");
    Console.WriteLine($"HORSE_LOAD_TEST_TOKEN {testToken}");
    Console.WriteLine($"HORSE_LOAD_TEST_APP_PROCESS_ID {Environment.ProcessId}");
    Console.WriteLine($"HORSE_LOAD_TEST_DATABASE_CONTAINER_ID {database.Id}");
    await app.WaitForShutdownAsync();
}
finally
{
    if (app is not null)
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    await database.DisposeAsync();
}

static int ReadPort()
{
    var value = Environment.GetEnvironmentVariable("HORSE_LOAD_TEST_PORT");
    return int.TryParse(value, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65_535
        ? port
        : 18_100;
}

static bool ReadBoolean(string name) =>
    bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value;
