using BotFramework.Sdk.Modules.Migrations;

namespace CasinoShiz.Identity;

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