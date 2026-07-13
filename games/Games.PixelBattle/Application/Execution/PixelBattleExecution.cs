using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.PixelBattle.Application.Execution;

public sealed record PixelBattleCommand(
    long UserId,
    int Index,
    string Color,
    string CommandId);

public sealed record PixelBattleTileState(int Index, string Color, long Version, long UpdatedBy);

public sealed record PixelBattleExecutionState(
    PixelBattleTileState? Tile,
    bool KnownUser,
    long NextVersion);

public sealed record PixelBattleTileUpdated(
    int Index,
    string Color,
    string Versionstamp,
    long UserId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pixelbattle.tile_updated";
}

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

public sealed class PixelBattleDescriptor
    : GameExecutionDescriptor<PixelBattleCommand, PixelBattleExecutionState, PixelUpdateResult>
{
    public override string GameId => "pixelbattle";
    public override bool UsesPrimaryWallet => false;
    public override string CommandId(PixelBattleCommand command) => command.CommandId;
    public override string AggregateId(PixelBattleCommand command) => $"tile:{command.Index}";
    public override long ChatId(PixelBattleCommand command) => 0;
    public override string DisplayName(PixelBattleCommand command) => "pixelbattle user";
    public override WalletIdentity Wallet(PixelBattleCommand command) => new(command.UserId, 0);
}
