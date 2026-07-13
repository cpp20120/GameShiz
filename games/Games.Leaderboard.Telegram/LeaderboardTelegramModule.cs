using Games.Leaderboard.Application.Handlers;
namespace Games.Leaderboard.Telegram;
public sealed class LeaderboardTelegramModule : IModule
{
    public string Id => "leaderboard";
    public string DisplayName => "🏆 Топ";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services.AddHandler<LeaderboardHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
