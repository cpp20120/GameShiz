namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record DesiredRestrictionRow(
    long ChatId,
    long UserId,
    string DesiredRestrictionJson,
    string? ObservedRestrictionJson);
