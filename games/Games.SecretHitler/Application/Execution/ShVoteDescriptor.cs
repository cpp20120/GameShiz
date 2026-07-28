using BotFramework.Host.Execution;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShVoteDescriptor : SecretHitlerDescriptor<ShVoteCommand, ShVoteResult>
{
    public override IReadOnlyList<string> EntropyNames => SecretHitlerExecutionRules.ReshuffleEntropyNames;
}
