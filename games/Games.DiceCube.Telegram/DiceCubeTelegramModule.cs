using Games.DiceCube.Application.Handlers;

namespace Games.DiceCube.Telegram;

public sealed class DiceCubeTelegramModule : IModule
{
    public string Id => "dicecube";
    public string DisplayName => "🎲 Куб";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddHandler<DiceCubeHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
