using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;

namespace Games.Transfer.Application.Execution;

public sealed class TransferDescriptor
    : GameExecutionDescriptor<TransferCommand, TransferState, TransferAttemptResult>
{
    public override string GameId => "transfer";
    public override string CommandId(TransferCommand command) => command.CommandId;
    public override string AggregateId(TransferCommand command) =>
        $"{command.ChatId}:{command.FromUserId}:{command.ToUserId}";
    public override long ChatId(TransferCommand command) => command.ChatId;
    public override string DisplayName(TransferCommand command) => command.SenderDisplayName;
    public override WalletIdentity Wallet(TransferCommand command) =>
        new(command.FromUserId, command.ChatId);
    public override IReadOnlyList<string> AdditionalLockKeys(TransferCommand command) =>
        [new WalletIdentity(command.ToUserId, command.ChatId).LockKey];
}
