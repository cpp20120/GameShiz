using Games.Transfer.Application.Handlers;

namespace Games.Transfer.Telegram;

public sealed class TransferTelegramModule : IModule
{
    public string Id => "transfer";
    public string DisplayName => "💸 Перевод";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) => services.AddHandler<TransferHandler>();
    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}
