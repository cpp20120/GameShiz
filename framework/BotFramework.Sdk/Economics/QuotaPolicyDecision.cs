namespace BotFramework.Sdk.Economics;

public sealed record QuotaPolicyDecision(
    bool Applied,
    bool Rejected,
    long NewUsed,
    long Limit);
