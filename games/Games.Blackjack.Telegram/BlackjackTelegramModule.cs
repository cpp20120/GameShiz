using Games.Blackjack.Application.Handlers;
namespace Games.Blackjack.Telegram;
public sealed class BlackjackTelegramModule : IModule
{
    public string Id => "blackjack";
    public string DisplayName => "🃏 Блэкджек";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services.AddHandler<BlackjackHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
