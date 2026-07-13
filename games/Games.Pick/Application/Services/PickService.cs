using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;
using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Services;

public sealed partial class PickService(
    IAtomicGameExecutor<PickCommand, PickGameState, PickResult> executor,
    PickChainStore chains,
    IOptions<PickOptions> options,
    ILogger<PickService> logger) : IPickService
{
    private readonly PickOptions opts = options.Value;

    public Task<PickResult> PickAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        CancellationToken ct) =>
        PickAsync(userId, displayName, chatId, amount, variants, backedIndices, 0, ct);

    public Task<PickResult> PickAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        int sourceMessageId,
        CancellationToken ct)
    {
        var commandId = sourceMessageId != 0
            ? $"pick:roll:{chatId}:{sourceMessageId}:{userId}"
            : $"pick:roll:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        return ExecuteAsync(userId, displayName, chatId, amount, variants, backedIndices,
            depth: 0, applyStreak: true, commandId, ct);
    }

    public Task<PickResult> ContinueChainAsync(PickChainState chain, CancellationToken ct) =>
        ExecuteAsync(
            chain.UserId,
            chain.DisplayName,
            chain.ChatId,
            chain.StakeForNext,
            chain.Variants,
            chain.BackedIndices,
            chain.Depth,
            applyStreak: false,
            $"pick:chain:{chain.Id:N}",
            ct);

    public Task<PickChainState?> ClaimChainAsync(Guid chainId, CancellationToken ct) =>
        chains.ClaimAsync(chainId, ct);

    public Task RestoreChainAsync(PickChainState chain, CancellationToken ct) =>
        chains.RestoreAsync(chain, ct);

    private async Task<PickResult> ExecuteAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        int depth,
        bool applyStreak,
        string commandId,
        CancellationToken ct)
    {
        var command = new PickCommand(
            userId, displayName, chatId, amount, variants, backedIndices, depth, applyStreak,
            commandId, opts.MinVariants, opts.MaxVariants, opts.MaxBet, opts.HouseEdge,
            opts.StreakBonusPerWin, opts.StreakCap, opts.ChainMaxDepth, opts.ChainTtlSeconds);
        var result = await executor.ExecuteAsync(new(command), ct).ConfigureAwait(false);
        if (result.Error == PickError.None)
        {
            LogRoll(
                userId, chatId, amount, variants.Count, backedIndices.Count,
                result.PickedIndex, result.Won, result.Payout, depth);
        }
        return result;
    }

    [LoggerMessage(EventId = 5910, Level = LogLevel.Information,
        Message = "pick.roll user={UserId} chat={ChatId} bet={Bet} N={Variants} k={Backed} idx={Picked} won={Won} payout={Payout} depth={Depth}")]
    partial void LogRoll(long userId, long chatId, int bet, int variants, int backed, int picked, bool won, int payout, int depth);
}
