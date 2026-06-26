using Dapper;

namespace Games.Horse.Infrastructure.Persistence;

public sealed record HorseResultRow(string RaceDate, long BalanceScopeId, int Winner, string? FileId);
