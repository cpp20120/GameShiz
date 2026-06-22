using BotFramework.Host;
using Dapper;

namespace Games.Horse;

public sealed record HorseBetRow(
    Guid Id, string RaceDate, long UserId, long BalanceScopeId, int HorseId, int Amount);
