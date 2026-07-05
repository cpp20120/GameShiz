namespace Games.Redeem.Telegram;

public sealed class RedeemTelegramModule : IModule
{
    public string Id => "redeem";
    public string DisplayName => "🎟 Redeem";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services
        .AddSingleton<RedeemCaptchaTimeouts>()
        .AddHandler<RedeemHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
