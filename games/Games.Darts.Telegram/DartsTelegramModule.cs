using Games.Darts.Application.Handlers;

namespace Games.Darts.Telegram;

public sealed class DartsTelegramModule : IModule
{
    public string Id => "darts";
    public string DisplayName => "🎯 Дартс";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddHandler<DartsHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
