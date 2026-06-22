using BotFramework.Host;
using Dapper;

namespace Games.Horse;

public sealed record HorseResultRow(string RaceDate, long BalanceScopeId, int Winner, string? FileId);
