using BotFramework.Contracts.Operations;

namespace BotFramework.Host.Economics.Services;

public sealed class EconomySimulationService : IEconomySimulationService
{
    public EconomySimulationReport Simulate(EconomySimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = request.Rules;
        if (request.Players is < 1 or > 1_000_000 || request.Rounds is < 1 or > 10_000_000)
            throw new ArgumentOutOfRangeException(nameof(request), "Players or rounds are outside safe limits.");
        if (rules.StartingBalance < 0 || rules.Stake <= 0 || rules.WinPayout < 0 ||
            rules.WinProbability is < 0 or > 1)
            throw new ArgumentException("The economy rules are invalid.", nameof(request));

        var random = new System.Random(request.Seed);
        var balances = Enumerable.Repeat(rules.StartingBalance, request.Players).ToArray();
        long stakes = 0;
        long payouts = 0;
        for (var round = 0; round < request.Rounds; round++)
        {
            var player = round % request.Players;
            if (balances[player] < rules.Stake)
                continue;
            balances[player] -= rules.Stake;
            stakes += rules.Stake;
            if (random.NextDouble() < rules.WinProbability)
            {
                balances[player] += rules.WinPayout;
                payouts += rules.WinPayout;
            }
        }

        var net = payouts - stakes;
        var warnings = new List<string>();
        if (net > rules.InflationWarningThreshold)
            warnings.Add($"Inflation warning: net emission {net} exceeds {rules.InflationWarningThreshold}.");
        if (stakes > 0 && (double)payouts / stakes > 1)
            warnings.Add("Configured/effective RTP is above 100%.");
        return new(Math.Max(0, net), Math.Max(0, -net), stakes == 0 ? 0 : (double)payouts / stakes,
            balances, warnings);
    }
}
