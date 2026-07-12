using BotFramework.Contracts.Operations;
using BotFramework.Host.Economics.Services;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class OperatorToolsTests
{
    [Fact]
    public void FrameworkMigrations_HaveUniqueIdsAndNewTablesAreIdempotent()
    {
        var migrations = new BotFramework.Host.Composition.Migrations.FrameworkMigrations().Migrations;

        Assert.Equal(migrations.Count, migrations.Select(migration => migration.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(migrations, migration => migration.Id == "020_game_availability"
            && migration.Sql.Contains("IF NOT EXISTS game_availability_overrides", StringComparison.Ordinal));
        Assert.Contains(migrations, migration => migration.Id == "021_fairness_audit"
            && migration.Sql.Contains("IF NOT EXISTS fairness_audit", StringComparison.Ordinal));
    }

    [Fact]
    public void EconomySimulation_IsBitwiseDeterministicForSameSnapshotAndSeed()
    {
        var service = new EconomySimulationService();
        var request = new EconomySimulationRequest(new(100, 10, 18, 0.5, 1_000), 50, 10_000, 1729);

        var first = service.Simulate(request);
        var second = service.Simulate(request);

        Assert.Equal(first.Emission, second.Emission);
        Assert.Equal(first.Sinks, second.Sinks);
        Assert.Equal(first.Rtp, second.Rtp);
        Assert.Equal(first.FinalBalances, second.FinalBalances);
        Assert.Equal(first.Warnings, second.Warnings);
    }

    [Fact]
    public void EconomySimulation_ChangesWhenSeedChanges()
    {
        var service = new EconomySimulationService();
        var rules = new EconomyRulesSnapshot(100, 10, 18, 0.5, 1_000);

        var first = service.Simulate(new(rules, 50, 10_000, 1));
        var second = service.Simulate(new(rules, 50, 10_000, 2));

        Assert.NotEqual(first.FinalBalances, second.FinalBalances);
    }

    [Fact]
    public void EconomySimulation_RejectsUnsafeWorkloads()
    {
        var service = new EconomySimulationService();
        var request = new EconomySimulationRequest(new(100, 10, 18, 0.5, 1_000), 1, 10_000_001, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Simulate(request));
    }
}
