using BotFramework.Sdk.Modules.Migrations;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class ChatAdministrationMigrations : IModuleMigrations
{
    public string ModuleId => "chat_administration";

    public IReadOnlyList<Migration> Migrations =>
    [
        new Migration("001_initial", ChatAdministrationSchema.Sql),
    ];
}
