using BotFramework.Contracts.Messaging;

namespace Games.Dice.Contracts.Play;

public sealed record DicePlayRequest(
    long UserId,
    string DisplayName,
    int SlotValue,
    long BalanceScopeId,
    string OperationSourceId,
    bool IsForwarded) : IRequest<DicePlayResponse>
{
    public string MessageType => "dice.play.v1";
}
