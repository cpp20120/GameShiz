using BotFramework.Host.Execution;

namespace Games.Poker.Application.Execution;

public sealed class PokerStartDescriptor : PokerDescriptor<PokerStartCommand, StartResult>
{
    public override IReadOnlyList<string> EntropyNames => PokerExecutionRules.ShuffleEntropyNames;
}
