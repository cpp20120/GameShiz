using BotFramework.Host.Execution;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

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

public sealed class TransferStateStore(IOptions<BotFrameworkOptions> frameworkOptions)
    : IGameStateStore<TransferCommand, TransferState>
{
    public async Task<TransferState> LoadAsync(
        TransferCommand command, IGameExecutionContext context, CancellationToken ct)
    {
        var displayName = command.RecipientDisplayName.Length > 64
            ? command.RecipientDisplayName[..64]
            : command.RecipientDisplayName;
        await context.ExecuteAsync("""
            INSERT INTO users (telegram_user_id,balance_scope_id,display_name,coins)
            VALUES (@ToUserId,@ChatId,@displayName,@startingCoins)
            ON CONFLICT (telegram_user_id,balance_scope_id)
            DO UPDATE SET display_name=EXCLUDED.display_name,updated_at=now()
            """, new
        {
            command.ToUserId,
            command.ChatId,
            displayName,
            startingCoins = frameworkOptions.Value.StartingCoins,
        }, ct);
        var balance = await context.QuerySingleOrDefaultAsync<int?>("""
            SELECT coins FROM users
            WHERE telegram_user_id=@ToUserId AND balance_scope_id=@ChatId
            FOR UPDATE
            """, new { command.ToUserId, command.ChatId }, ct)
            ?? throw new InvalidOperationException("Recipient wallet was not created.");
        return new(balance);
    }

    public Task SaveAsync(
        TransferCommand command, TransferState state, IGameExecutionContext context, CancellationToken ct) =>
        Task.CompletedTask;
}
