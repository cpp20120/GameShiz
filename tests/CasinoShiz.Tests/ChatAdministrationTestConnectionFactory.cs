using BotFramework.Host.Persistence.Connections;
using Npgsql;

namespace CasinoShiz.Tests;

internal sealed class ChatAdministrationTestConnectionFactory(string connectionString) : INpgsqlConnectionFactory
{
    public NpgsqlConnection Create() => new(connectionString);

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = Create();
        await connection.OpenAsync(ct);
        return connection;
    }
}
