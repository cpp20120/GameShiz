
namespace Games.Transfer.Infrastructure.Migrations;

public sealed class TransferMigrations : IModuleMigrations
{
    public string ModuleId => "transfer";

    public IReadOnlyList<Migration> Migrations { get; } = [];
}
