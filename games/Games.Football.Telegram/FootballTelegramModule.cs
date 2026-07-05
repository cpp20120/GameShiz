using Games.Football.Application.Handlers;

namespace Games.Football.Telegram;

public sealed class FootballTelegramModule : IModule
{
    public string Id => "football";
    public string DisplayName => "⚽ Футбол";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddHandler<FootballHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
