using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Blackjack.Application.Execution;
using Microsoft.Extensions.Options;

namespace Games.Blackjack.Application.Services;

public sealed class BlackjackService(
    IAtomicGameExecutor<BlackjackStartCommand, BlackjackGameState, BlackjackResult> startExecutor,
    IAtomicGameExecutor<BlackjackTurnCommand, BlackjackGameState, BlackjackResult> turnExecutor,
    IAtomicGameExecutor<BlackjackSetMessageCommand, BlackjackGameState, BlackjackResult> messageExecutor,
    IBlackjackStateReader states,
    IWalletReadService wallets,
    IOptions<BlackjackOptions> options) : IBlackjackService
{
    private readonly BlackjackOptions gameOptions = options.Value;

    public Task<BlackjackResult> StartAsync(
        long userId,
        string displayName,
        long chatId,
        int bet,
        string operationId,
        CancellationToken ct) =>
        startExecutor.ExecuteAsync(
            new GameExecutionEnvelope<BlackjackStartCommand>(
                new(
                    userId,
                    displayName,
                    chatId,
                    bet,
                    operationId,
                    gameOptions.MinBet,
                    gameOptions.MaxBet,
                    gameOptions.HandTimeoutMs)),
            ct);

    public Task<BlackjackResult> HitAsync(long userId, CancellationToken ct) =>
        ExecuteTurnAsync(userId, BlackjackTurnKind.Hit, ct);

    public Task<BlackjackResult> StandAsync(long userId, CancellationToken ct) =>
        ExecuteTurnAsync(userId, BlackjackTurnKind.Stand, ct);

    public Task<BlackjackResult> DoubleAsync(long userId, CancellationToken ct) =>
        ExecuteTurnAsync(userId, BlackjackTurnKind.DoubleDown, ct);

    public async Task<(BlackjackSnapshot? snapshot, int? stateMessageId)> GetSnapshotAsync(
        long userId,
        CancellationToken ct)
    {
        var state = await states.LoadAsync(userId, ct).ConfigureAwait(false);
        if (state?.Status != TurnGameStatus.Active || state.Hand is not { } hand)
            return (null, null);
        var wallet = await wallets.GetAsync(userId, hand.ChatId, ct).ConfigureAwait(false);
        return (
            BlackjackDecisionRules.BuildSnapshot(hand, wallet?.Coins ?? 0, revealed: false),
            hand.StateMessageId);
    }

    public async Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct)
    {
        var state = await states.LoadAsync(userId, ct).ConfigureAwait(false);
        if (state?.Status != TurnGameStatus.Active || state.Hand is not { } hand)
            return;
        var commandId = string.Create(
            CultureInfo.InvariantCulture,
            $"blackjack:message:{hand.HandId}:{messageId}");
        await messageExecutor.ExecuteAsync(
            new GameExecutionEnvelope<BlackjackSetMessageCommand>(
                new(
                    userId,
                    state.DisplayName,
                    hand.ChatId,
                    hand.HandId,
                    messageId,
                    commandId)),
            ct).ConfigureAwait(false);
    }

    private async Task<BlackjackResult> ExecuteTurnAsync(
        long userId,
        BlackjackTurnKind kind,
        CancellationToken ct)
    {
        var state = await states.LoadAsync(userId, ct).ConfigureAwait(false);
        if (state?.Status != TurnGameStatus.Active || state.Hand is not { } hand)
            return new BlackjackResult(BlackjackError.NoActiveHand, null);
        var commandId = string.Create(
            CultureInfo.InvariantCulture,
            $"blackjack:{kind.ToString().ToLowerInvariant()}:{hand.HandId}:{state.Revision}");
        return await turnExecutor.ExecuteAsync(
            new GameExecutionEnvelope<BlackjackTurnCommand>(
                new(
                    userId,
                    state.DisplayName,
                    hand.ChatId,
                    kind,
                    state.Revision,
                    commandId)),
            ct).ConfigureAwait(false);
    }
}
