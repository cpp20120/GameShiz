using Games.Bowling.Application.Handlers;

namespace Games.Bowling.Telegram;

public sealed class BowlingTelegramModule : IModule
{
    public string Id => "bowling";
    public string DisplayName => "🎳 Боулинг";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddHandler<BowlingHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
