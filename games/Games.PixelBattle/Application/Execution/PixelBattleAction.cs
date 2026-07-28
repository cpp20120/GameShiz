using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.PixelBattle.Application.Execution;

public sealed class PixelBattleAction
    : IGameAction<PixelBattleCommand, PixelBattleExecutionState, PixelUpdateResult>
{
    public GameDecision<PixelBattleExecutionState, PixelUpdateResult> Decide(
        GameActionInput<PixelBattleExecutionState, PixelBattleCommand> input)
    {
        if (!PixelBattleConstants.IsValidIndex(input.Command.Index))
            return Reject(input.State, PixelUpdateStatus.InvalidIndex);
        if (!PixelBattleConstants.IsValidColor(input.Command.Color))
            return Reject(input.State, PixelUpdateStatus.InvalidColor);
        if (!input.State.KnownUser)
            return Reject(input.State, PixelUpdateStatus.UnknownUser);
        if (input.State.NextVersion <= 0)
            throw new InvalidOperationException("Pixel version was not allocated by the execution state store.");

        var versionstamp = input.State.NextVersion.ToString("D20", CultureInfo.InvariantCulture);
        var update = new PixelBattleUpdate(input.Command.Index, input.Command.Color, versionstamp);
        var state = new PixelBattleExecutionState(
            new(input.Command.Index, input.Command.Color, input.State.NextVersion, input.Command.UserId),
            true,
            input.State.NextVersion);
        return new(DecisionStatus.Accepted, state, new(PixelUpdateStatus.Updated, update),
            [], [], [], [new PixelBattleTileUpdated(input.Command.Index, input.Command.Color,
                versionstamp, input.Command.UserId, input.UtcNow.ToUnixTimeMilliseconds())], []);
    }

    private static GameDecision<PixelBattleExecutionState, PixelUpdateResult> Reject(
        PixelBattleExecutionState state, PixelUpdateStatus status) =>
        new(DecisionStatus.Rejected, state, new(status), [], [], [], [], [], status.ToString());
}
