using BotFramework.Sdk;

namespace Games.Poker;

public sealed record PokerHandEnded(
    string InviteCode,
    string Reason,
    IReadOnlyList<(long UserId, int Amount)> Winners,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "poker.hand_ended";
}
