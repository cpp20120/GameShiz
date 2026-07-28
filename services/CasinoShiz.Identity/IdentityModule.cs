using BotFramework.Contracts.Identity;
using BotFramework.Sdk.Modules;
using BotFramework.Sdk.Modules.Migrations;

namespace CasinoShiz.Identity;

public sealed class IdentityModule : IModule
{
    public string Id => "identity";
    public string DisplayName => "Player Identity";
    public string Version => "1.0.0";
    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddSingleton<IPlayerDirectory, PlayerDirectory>();
    public IModuleMigrations GetMigrations() => new IdentityMigrations();
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() => [];
}