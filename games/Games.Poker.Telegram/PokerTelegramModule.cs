namespace Games.Poker.Telegram;

public sealed class PokerTelegramModule : IModule
{
    public string Id => "poker";
    public string DisplayName => "🃏 Покер";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services
            .AddHandler<PokerHandler>()
            .AddRecurringScheduledCommand<PokerTurnTimeoutJob>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/poker", "poker.cmd.poker"),
    ];

    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
