using Npgsql;

namespace BotFramework.Host.Persistence.Connections;

public interface INpgsqlConnectionFactory
{
    NpgsqlConnection Create();
    Task<NpgsqlConnection> OpenAsync(CancellationToken ct);
}
