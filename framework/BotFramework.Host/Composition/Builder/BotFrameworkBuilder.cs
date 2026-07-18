using Microsoft.Extensions.Configuration;

namespace BotFramework.Host.Composition.Builder;

public sealed class BotFrameworkBuilder : IBotFrameworkBuilder
{
    private readonly List<IModule> _modules = [];
    private readonly Dictionary<string, Dictionary<string, string>> _locales = new(StringComparer.Ordinal);
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
                _locales[bundle.CultureCode] = dict = new(StringComparer.Ordinal);

            foreach (var (key, value) in bundle.Strings)
                dict[$"{module.Id}.{key}"] = value;
        }

        foreach (var command in module.GetBotCommands())
        {
            if (!_botCommands.Any(existing => string.Equals(existing.Command, command.Command, StringComparison.Ordinal)))
                _botCommands.Add(command);
        }
        _modules.Add(module);

        return this;
    }
}
