// ─────────────────────────────────────────────────────────────────────────────
// BotFrameworkBuilder — the ASP.NET-Core-style composition surface for a Host.
//
// Usage:
//
//   var builder = WebApplication.CreateBuilder(args);
//
//   builder.AddBotFramework()
//       .AddModule<DiceModule>()
//       .AddModule<PokerModule>();
//
//   var app = builder.Build();
//   app.UseBotFramework();
//   app.Run();
//
// Why a builder and not a single `AddModules(params Type[])` call:
//   modules declare fluent options (future: AddModule<T>().WithOptions(...))
//   and the chainable style mirrors ASP.NET Core's AddAuthentication/AddJwtBearer
//   pattern the user already knows. New module authors get a familiar shape.
//
// Phase ordering inside the builder:
//   1. AddBotFramework() — registers framework singletons (Telegram client,
//      UpdateRouter, UpdatePipeline, the three default update middlewares,
//      BotHostedService) and constructs the ModuleServiceCollectionAdapter
//      modules will see during ConfigureServices.
//   2. AddModule<T>() — instantiates the module (parameterless ctor),
//      registers it as IModule in DI, calls module.ConfigureServices(adapter)
//      so its handlers/aggregates/projections/commands are wired, and folds
//      its locales/commands/migrations into the builder-local aggregate.
//   3. builder.Build() — the container is built. LoadedModules is resolved
//      from a singleton factory that closes over the aggregate built in (2),
//      so by the time BotHostedService asks for it every module is there.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host.Pipeline;
using BotFramework.Host.Commands;
using BotFramework.Host.Redis;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Sdk;
using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Telegram.Bot;
using Microsoft.AspNetCore.Http;

namespace BotFramework.Host.Composition;

internal sealed class BotFrameworkBuilder : IBotFrameworkBuilder
{
    private readonly List<IModule> _modules = [];
    private readonly Dictionary<string, Dictionary<string, string>> _locales = new();
    private readonly List<BotCommand> _botCommands = [];
    private readonly List<IModuleMigrations> _migrations = [];
    private readonly ModuleServiceCollectionAdapter _adapter;

    public IServiceCollection Services { get; }
    public IConfiguration Configuration { get; }

    public BotFrameworkBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;
        var registrations = new ModuleRegistrations();
        _adapter = new ModuleServiceCollectionAdapter(services, configuration, registrations);

        services.AddSingleton(registrations);

        services.AddSingleton<LoadedModules>(_ => new LoadedModules(
            _modules,
            _locales,
            _botCommands,
            _migrations));
    }

    public IBotFrameworkBuilder AddModule<TModule>() where TModule : class, IModule, new()
    {
        var module = new TModule();

        Services.AddSingleton<IModule>(module);
        module.ConfigureServices(_adapter);

        if (module.GetMigrations() is { } m)
            _migrations.Add(m);

        foreach (var bundle in module.GetLocales())
        {
            if (!_locales.TryGetValue(bundle.CultureCode, out var dict))
                _locales[bundle.CultureCode] = dict = new();

            foreach (var (key, value) in bundle.Strings)
                dict[$"{module.Id}.{key}"] = value;
        }

        _botCommands.AddRange(module.GetBotCommands());
        _modules.Add(module);

        return this;
    }
}
