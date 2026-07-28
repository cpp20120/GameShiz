using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

public sealed class RedeemIssueAction
    : IGameAction<RedeemIssueCommand, RedeemExecutionState, Guid>
{
    public GameDecision<RedeemExecutionState, Guid> Decide(
        GameActionInput<RedeemExecutionState, RedeemIssueCommand> input)
    {
        if (input.State.Code is not null)
            return new(DecisionStatus.Rejected, input.State, Guid.Empty, [], [], [], [], [], "code_exists");
        var code = new RedeemCode
        {
            Code = input.Command.Code, Active = true, IssuedBy = input.Command.IssuedBy,
            IssuedAt = input.UtcNow.ToUnixTimeMilliseconds(), FreeSpinGameId = input.Command.FreeSpinGameId,
        };
        return new(DecisionStatus.Accepted, new(code), code.Code, [], [], [],
            [new RedeemCodeIssued(code.Code, code.IssuedBy, code.FreeSpinGameId, code.IssuedAt)], []);
    }
}
