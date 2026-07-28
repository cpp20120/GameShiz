using BotFramework.Host.Execution;

namespace Games.Poker.Application.Execution;

public sealed class PokerCreateDescriptor : PokerDescriptor<PokerCreateCommand, CreateResult>
{
    public override IReadOnlyList<string> EntropyNames => [PokerExecutionRules.InviteEntropy];
}
