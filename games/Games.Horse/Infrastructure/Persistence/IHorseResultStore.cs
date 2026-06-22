using BotFramework.Host;
using Dapper;

namespace Games.Horse;

public interface IHorseResultStore
{
    Task<HorseResultRow?> FindAsync(string raceDate, long balanceScopeId, CancellationToken ct);
    Task UpsertAsync(HorseResultRow result, CancellationToken ct);
    Task SaveFileIdAsync(string raceDate, long balanceScopeId, string fileId, CancellationToken ct);
}
