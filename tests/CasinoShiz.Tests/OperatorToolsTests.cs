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
        Assert.Contains(migrations, migration => migration.Id == "036_multi_game_mini_game_sessions"
            && migration.Sql.Contains("PRIMARY KEY (user_id, chat_id, game_id)", StringComparison.Ordinal));
    }

    [Fact]
    public void MicroserviceMigrationSets_KeepWalletTablesOutOfBackend()
    {
        var framework = new BotFramework.Host.Composition.Migrations.FrameworkMigrations();
        var backend = framework.MicroservicesBackendMigrations;
        var wallet = framework.WalletMigrations;

        Assert.DoesNotContain(backend, migration => migration.Id is
            "003_users" or "006_per_chat_wallets_and_ledger" or "008_users_last_daily_bonus"
            or "009_users_telegram_dice_daily" or "014_economics_operation_id"
            or "017_responsible_gaming_and_ops_reports");
        Assert.Contains(wallet, migration => migration.Id == "006_per_chat_wallets_and_ledger");
        Assert.Contains(wallet, migration => migration.Id == "027_wallet_player_protection");
        Assert.DoesNotContain(backend, migration => migration.Sql.Contains("CREATE TABLE users", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(backend, migration => migration.Sql.Contains("CREATE TABLE player_protection", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(backend, migration => migration.Id == "027_operations_report_checkpoint");
        Assert.Contains(backend, migration => migration.Id == "035_durable_workflow_steps");
        Assert.Contains(backend, migration => migration.Id == "036_multi_game_mini_game_sessions");
        Assert.DoesNotContain(wallet, migration => migration.Id == "035_durable_workflow_steps");
        Assert.DoesNotContain(wallet, migration => migration.Id == "036_multi_game_mini_game_sessions");
        Assert.DoesNotContain(backend, migration => migration.Sql.Contains("player_identities", StringComparison.OrdinalIgnoreCase));
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
