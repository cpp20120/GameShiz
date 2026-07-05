namespace Games.Darts.Infrastructure.Modules;

/// <summary>Backend composition without Telegram roll-delivery worker.</summary>
public sealed class DartsRemoteModule : IModule
{
    public string Id => "darts";
    public string DisplayName => "Darts backend";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services)
    {
        services
            .BindOptions<DartsOptions>(DartsOptions.SectionName)
            .AddSingleton<IDartsRollQueue, ClientDeliveredDartsRollQueue>()
            .AddScoped<IDartsRoundStore, DartsRoundStore>()
            .AddScoped<IDartsService, DartsService>();
    }

    public IModuleMigrations GetMigrations() => new DartsMigrations();
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
