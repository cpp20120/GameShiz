using BotFramework.Sdk;

namespace Games.Redeem;

public sealed record RedeemCodeIssued(Guid Code, long IssuedBy, string FreeSpinGameId, long OccurredAt) : IDomainEvent
{
    public string EventType => "redeem.issued";
}
