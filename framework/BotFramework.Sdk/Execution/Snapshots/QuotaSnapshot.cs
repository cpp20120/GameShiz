namespace BotFramework.Sdk.Execution;

public sealed record QuotaSnapshot(long Used, long Limit)
{
    public long Remaining => Math.Max(0, Limit - Used);
}
