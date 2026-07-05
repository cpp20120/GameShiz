using Games.PixelBattle.Application.Handlers;
namespace Games.PixelBattle.Telegram;
public sealed class PixelBattleTelegramModule : IModule
{
    public string Id => "pixelbattle";
    public string DisplayName => "PixelBattle";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services.AddHandler<PixelBattleHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
