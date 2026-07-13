using Games.Basketball.Application.Handlers;

namespace Games.Basketball.Telegram;

public sealed class BasketballTelegramModule : IModule
{
    public string Id => "basketball";
    public string DisplayName => "🏀 Баскетбол";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddHandler<BasketballHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
