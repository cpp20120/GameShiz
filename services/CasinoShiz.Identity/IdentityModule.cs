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

public sealed class IdentityMigrations : IModuleMigrations
{
    public string ModuleId => "identity";
    public IReadOnlyList<Migration> Migrations { get; } =
    [
        new Migration("001_player_identities", """
            CREATE TABLE IF NOT EXISTS player_identities (
                telegram_user_id BIGINT PRIMARY KEY,
                display_name TEXT NOT NULL,
                username TEXT,
                first_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                last_seen_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_player_identities_username
                ON player_identities (lower(username)) WHERE username IS NOT NULL;
            """),
    ];
}
