using BotFramework.Host.Execution;

namespace Games.Horse.Application.Execution;

public sealed class HorsePlaceBetDescriptor
    : GameExecutionDescriptor<HorsePlaceBetCommand, HorseBetState, BetResult>
{
    public override string GameId => "horse";
    public override string CommandId(HorsePlaceBetCommand command) => command.CommandId;
    public override string AggregateId(HorsePlaceBetCommand command) =>
        $"{command.RaceDate}:{command.BalanceScopeId}:{command.UserId}";
    public override long ChatId(HorsePlaceBetCommand command) => command.BalanceScopeId;
    public override string DisplayName(HorsePlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(HorsePlaceBetCommand command) =>
        new(command.UserId, command.BalanceScopeId);
}

public sealed class HorseRunDescriptor
    : GameExecutionDescriptor<HorseRunCommand, HorseRaceState, RaceOutcome>
{
    public override string GameId => "horse";
    public override bool UsesPrimaryWallet => false;
    public override IReadOnlyList<string> EntropyNames => [HorseRunAction.WinnerEntropy];
    public override string CommandId(HorseRunCommand command) => command.CommandId;
    public override string AggregateId(HorseRunCommand command) =>
        $"{command.RaceDate}:{command.Kind}:{command.ResultScopeId}";
    public override long ChatId(HorseRunCommand command) => command.ResultScopeId;
    public override string DisplayName(HorseRunCommand command) => "horse race";
    public override WalletIdentity Wallet(HorseRunCommand command) => new(0, command.ResultScopeId);
    public override IReadOnlyList<string> AdditionalLockKeys(HorseRunCommand command) =>
        command.ExpectedBets
            .Select(bet => new WalletIdentity(bet.UserId, bet.BalanceScopeId).LockKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
