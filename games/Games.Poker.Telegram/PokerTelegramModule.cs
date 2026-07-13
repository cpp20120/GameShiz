namespace Games.Poker.Telegram;

using BotFramework.Rendering;
using Games.Poker.Infrastructure.Rendering;

public sealed class PokerTelegramModule : IModule
{
    public string Id => "poker";
    public string DisplayName => "🃏 Покер";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services
            .AddScoped<IRenderJob<PokerBoardRenderSpec>, PokerBoardRenderJob>()
            .AddHandler<PokerHandler>()
            .AddRecurringScheduledCommand<PokerTurnTimeoutJob>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/poker", "poker.cmd.poker"),
    ];

    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
