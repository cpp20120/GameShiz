namespace Games.Admin.Telegram;

public sealed class AdminTelegramModule : IModule
{
    public string Id => "admin";
    public string DisplayName => "🛠 Admin";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services
        .BindOptions<AdminOptions>(AdminOptions.SectionName)
        .AddHandler<AdminHandler>()
        .AddHandler<AnalyticsHandler>()
        .AddHandler<ChatsHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
