using BotFramework.Contracts.Messaging;

namespace BotFramework.Sdk.Events;

/// <summary>Requests a promo-code drop for the channel that completed a game.</summary>
public sealed record MiniGameRedeemCodeDropRequested(
    long UserId,
    long ChatId,
    string GameId,
    long OccurredAt,
    BotChannel Channel = BotChannel.Telegram) : IDomainEvent
{
    public string EventType => "minigame.redeem_code_drop_requested";
}
