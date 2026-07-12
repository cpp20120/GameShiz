using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed record BlackjackTurnCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    BlackjackTurnKind Kind,
    long ExpectedRevision,
    string CommandId) : ITurnGameCommand<long>
{
    public long PlayerId => UserId;
}
