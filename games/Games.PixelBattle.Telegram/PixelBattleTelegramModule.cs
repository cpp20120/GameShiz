using Games.PixelBattle.Application.Handlers;
namespace Games.PixelBattle.Telegram;
public sealed class PixelBattleTelegramModule : IModule
{
    public string Id => "pixelbattle";
    public string DisplayName => "PixelBattle";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services.AddHandler<PixelBattleHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/pixelbattle", "pixelbattle.cmd.pixelbattle"),
    ];
    public IReadOnlyList<LocaleBundle> GetLocales() =>
    [
        new LocaleBundle("ru", new Dictionary<string, string>
(StringComparer.Ordinal) {
            ["display_name"] = "PixelBattle",
            ["cmd.pixelbattle"] = "Открыть PixelBattle",
            ["open.text"] = "Открой PixelBattle через кнопку ниже. Рисовать могут только пользователи, уже известные боту.",
            ["open.button"] = "Открыть PixelBattle",
            ["open.not_configured"] = "PixelBattle WebApp URL не настроен. Укажи Games:pixelbattle:WebAppUrl.",
        }),
    ];
}
