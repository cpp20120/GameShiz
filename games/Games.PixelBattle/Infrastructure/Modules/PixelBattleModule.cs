
namespace Games.PixelBattle.Infrastructure.Modules;

using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.PixelBattle.Application.Execution;

public sealed class PixelBattleModule : IModule
{
    public string Id => "pixelbattle";
    public string DisplayName => "PixelBattle";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services)
    {
        services
            .BindOptions<PixelBattleOptions>(PixelBattleOptions.SectionName)
            .AddScoped<IPixelBattleStore, PixelBattleStore>()
            .AddScoped<IPixelBattleService, PixelBattleService>()
            .AddScoped<IPixelBattleCommandService, PixelBattleService>()
            .AddScoped<IGameAction<PixelBattleCommand, PixelBattleExecutionState, PixelUpdateResult>,
                PixelBattleAction>()
            .AddScoped<GameExecutionDescriptor<PixelBattleCommand, PixelBattleExecutionState, PixelUpdateResult>,
                PixelBattleDescriptor>()
            .AddScoped<IGameStateStore<PixelBattleCommand, PixelBattleExecutionState>,
                PixelBattleExecutionStateStore>()
            .AddSingleton<ITelegramWebAppInitDataValidator, TelegramWebAppInitDataValidator>()
            .AddSingleton<PixelBattleBroadcaster>();
    }

    public IModuleMigrations GetMigrations() => new PixelBattleMigrations();

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
