using BotFramework.Host.Execution;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShStartDescriptor : SecretHitlerDescriptor<ShStartCommand, ShStartResult>
{
    public override IReadOnlyList<string> EntropyNames =>
        [.. SecretHitlerExecutionRules.RoleEntropyNames, .. SecretHitlerExecutionRules.DeckEntropyNames];
}
