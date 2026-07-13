namespace Games.SecretHitler.Telegram;

public sealed class SecretHitlerTelegramModule : IModule
{
    public string Id => "sh";
    public string DisplayName => "🗳 Secret Hitler";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddHandler<SecretHitlerHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
