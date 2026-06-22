using BotFramework.Host;
using Dapper;

namespace Games.Horse;

public interface IHorseBetStore
{
    /// <summary>All bets for the calendar day (every chat). Used for global race.</summary>
    Task<IReadOnlyList<HorseBetRow>> ListByRaceDateAsync(string raceDate, CancellationToken ct);

    /// <summary>Bets for one balance scope (one Telegram chat) on that day.</summary>
    Task<IReadOnlyList<HorseBetRow>> ListByRaceDateAndScopeAsync(string raceDate, long balanceScopeId, CancellationToken ct);

    Task InsertAsync(HorseBetRow bet, CancellationToken ct);

    Task DeleteByRaceDateAsync(string raceDate, CancellationToken ct);
    Task DeleteByRaceDateAndScopeAsync(string raceDate, long balanceScopeId, CancellationToken ct);
}
