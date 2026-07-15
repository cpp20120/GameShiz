using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;

namespace Games.Transfer.Application.Execution;

public sealed record TransferCommand(
    long FromUserId,
    long ToUserId,
    long ChatId,
    string SenderDisplayName,
    string RecipientDisplayName,
    int NetToRecipient,
    int FeeCoins,
    int TotalDebited,
    string CommandId);

public sealed record TransferState(int RecipientBalance);

public sealed class TransferAction
    : IGameAction<TransferCommand, TransferState, TransferAttemptResult>
{
    public GameDecision<TransferState, TransferAttemptResult> Decide(
        GameActionInput<TransferState, TransferCommand> input)
    {
        var command = input.Command;
        if (input.Wallet.Balance < command.TotalDebited)
        {
            return new(
                DecisionStatus.Rejected,
                input.State,
                new TransferAttemptResult(TransferError.InsufficientFunds, command.NetToRecipient,
                    command.FeeCoins, command.TotalDebited, checked((int)input.Wallet.Balance), 0),
                [], [], [], [], [], "insufficient_funds");
        }

        return new(
            DecisionStatus.Accepted,
            input.State,
            new TransferAttemptResult(TransferError.None, command.NetToRecipient, command.FeeCoins,
                command.TotalDebited, checked((int)input.Wallet.Balance - command.TotalDebited),
                checked(input.State.RecipientBalance + command.NetToRecipient)),
            [EconomyEffect.Debit(command.TotalDebited, "transfer.send")],
            [], [],
            [new TransferCompletedMetaEvent(command.ChatId, command.FromUserId, command.ToUserId,
                command.TotalDebited, command.NetToRecipient, command.FeeCoins,
                input.UtcNow.ToUnixTimeMilliseconds())],
            [],
            CustomEffects:
            [
                WalletEconomyEffect.Credit(command.ToUserId, command.ChatId,
                    command.NetToRecipient, "transfer.receive"),
            ]);
    }
}

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

public sealed class TransferStateStore(IEconomicsService economics)
    : IGameStateStore<TransferCommand, TransferState>
{
    public async Task<TransferState> LoadAsync(
        TransferCommand command, IGameExecutionContext context, CancellationToken ct)
    {
        await economics.EnsureUserAsync(command.ToUserId, command.ChatId, command.RecipientDisplayName, ct);
        var balance = await economics.GetBalanceAsync(command.ToUserId, command.ChatId, ct);
        return new(balance);
    }

    public Task SaveAsync(
        TransferCommand command, TransferState state, IGameExecutionContext context, CancellationToken ct) =>
        Task.CompletedTask;
}
