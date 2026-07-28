using BotFramework.Host.Execution;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShCreateDescriptor : SecretHitlerDescriptor<ShCreateCommand, ShCreateResult>
{
    public override IReadOnlyList<string> EntropyNames => [SecretHitlerExecutionRules.InviteEntropy];
}
