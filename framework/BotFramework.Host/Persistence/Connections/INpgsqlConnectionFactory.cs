using Npgsql;

namespace BotFramework.Host.Persistence;

public interface INpgsqlConnectionFactory
{
    NpgsqlConnection Create();
    Task<NpgsqlConnection> OpenAsync(CancellationToken ct);
}
