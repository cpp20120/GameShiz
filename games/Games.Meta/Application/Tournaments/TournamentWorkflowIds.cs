using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

internal static class TournamentWorkflowIds
{
    public static string ForTournament(long tournamentId) =>
        $"tournament:{tournamentId.ToString(CultureInfo.InvariantCulture)}";
}
