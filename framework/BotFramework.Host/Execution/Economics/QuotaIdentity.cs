namespace BotFramework.Host.Execution;

using System.Globalization;

public sealed record QuotaIdentity(
    string QuotaId,
    string GameId,
    long UserId,
    long BalanceScopeId,
    DateOnly OnDate,
    long Limit)
{
    public string LockKey => string.Create(
        CultureInfo.InvariantCulture,
        $"quota:{GameId}:{BalanceScopeId}:{UserId}:{OnDate:yyyy-MM-dd}");
}
