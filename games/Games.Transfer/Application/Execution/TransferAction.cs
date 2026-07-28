using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;

namespace Games.Transfer.Application.Execution;

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
