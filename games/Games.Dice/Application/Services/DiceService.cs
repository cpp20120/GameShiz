// ─────────────────────────────────────────────────────────────────────────────
// Compatibility facade. Transport contracts still call IDiceService while the
// complete mutation path is delegated to AtomicGameExecutor.
// ─────────────────────────────────────────────────────────────────────────────


using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Dice.Application.Execution;

namespace Games.Dice.Application.Services;

public sealed class DiceService(
    IAtomicGameExecutor<DiceCommand, NoGameState, DicePlayResult> executor,
    IRuntimeTuningAccessor tuning) : IDiceService
{
    public async Task<DicePlayResult> PlayAsync(
        long userId,
        string displayName,
        int diceValue,
        long chatId,
        int sourceMessageId,
        bool isForwarded,
        CancellationToken ct)
    {
        if (isForwarded)
            return new DicePlayResult(DiceOutcome.Forwarded);

        var diceOpts = tuning.GetSection<DiceOptions>(DiceOptions.SectionName);
        var command = new DiceCommand(
            userId,
            displayName,
            diceValue,
            chatId,
            sourceMessageId,
            false,
            diceOpts.Cost,
            diceOpts.RedeemDropChance);
        return await executor.ExecuteAsync(new GameExecutionEnvelope<DiceCommand>(command), ct)
            .ConfigureAwait(false);
    }
}
