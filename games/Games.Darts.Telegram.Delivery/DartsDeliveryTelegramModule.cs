namespace Games.Darts.Telegram.Delivery;

public sealed class DartsDeliveryTelegramModule : IModule
{
    public string Id => "darts.delivery.telegram";
    public string DisplayName => "Darts Telegram delivery";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddSingleton<IDartsRollDelivery, DartsBotDiceSender>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
