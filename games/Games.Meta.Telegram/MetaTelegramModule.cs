namespace Games.Meta.Telegram;

public sealed class MetaTelegramModule : IModule
{
    public string Id => "meta";
    public string DisplayName => "⭐ Мета";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) => services
        .AddHandler<MetaMenuHandler>()
        .AddHandler<MetaHandler>()
        .AddHandler<TournamentHandler>()
        .AddHandler<RiskHandler>()
        .AddHandler<MyStatsHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
