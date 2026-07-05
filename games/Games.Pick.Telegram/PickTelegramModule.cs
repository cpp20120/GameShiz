namespace Games.Pick.Telegram;
public sealed class PickTelegramModule : IModule
{
    public string Id => "pick";
    public string DisplayName => "🎯 Выбор";
    public string Version => "2.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services
        .AddScoped<IPickAnnouncementPublisher, TelegramPickAnnouncementPublisher>()
        .AddHandler<PickHandler>()
        .AddHandler<PickLotteryHandler>()
        .AddHandler<PickDailyLotteryHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
