namespace Games.Horse.Telegram;
public sealed class HorseTelegramModule : IModule
{
    public string Id => "horse";
    public string DisplayName => "🐎 Скачки";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services
        .AddScoped<IHorseRaceNotifier, HorseRaceNotifier>()
        .AddHandler<HorseHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
