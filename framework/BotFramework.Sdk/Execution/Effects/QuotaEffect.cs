namespace BotFramework.Sdk.Execution;

public sealed record QuotaEffect(string QuotaId, QuotaEffectKind Kind, long Amount) : IGameEffect
{
    public static QuotaEffect Consume(string quotaId, long amount = 1) =>
        new(RequireQuotaId(quotaId), QuotaEffectKind.Consume, RequirePositive(amount));

    public static QuotaEffect Restore(string quotaId, long amount = 1) =>
        new(RequireQuotaId(quotaId), QuotaEffectKind.Restore, RequirePositive(amount));

    /// <summary>Grants future capacity and may move usage below zero.</summary>
    public static QuotaEffect Grant(string quotaId, long amount = 1) =>
        new(RequireQuotaId(quotaId), QuotaEffectKind.Grant, RequirePositive(amount));

    private static string RequireQuotaId(string quotaId) => !string.IsNullOrWhiteSpace(quotaId)
        ? quotaId
        : throw new ArgumentException("Quota id is required.", nameof(quotaId));

    private static long RequirePositive(long amount) => amount > 0
        ? amount
        : throw new ArgumentOutOfRangeException(nameof(amount), amount, "Effect amount must be positive.");
}
