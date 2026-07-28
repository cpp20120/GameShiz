using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;

namespace Games.Transfer.Application.Execution;

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
