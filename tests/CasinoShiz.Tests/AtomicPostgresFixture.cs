using BotFramework.Host.Composition.Migrations;
using Dapper;
using Games.Dice.Infrastructure.Migrations;
using Games.DiceCube.Infrastructure.Migrations;
using Games.Blackjack.Infrastructure.Migrations;
using Games.Basketball.Infrastructure.Migrations;
using Games.Bowling.Infrastructure.Migrations;
using Games.Football.Infrastructure.Migrations;
using Games.Pick.Infrastructure.Migrations;
using Games.Darts.Infrastructure.Migrations;
using Games.Horse.Infrastructure.Migrations;
using Games.Poker.Infrastructure.Migrations;
using Games.Challenges.Infrastructure.Migrations;
using Games.Redeem.Infrastructure.Migrations;
using Games.SecretHitler.Infrastructure.Migrations;
using Games.PixelBattle.Infrastructure.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CasinoShiz.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AtomicPostgresCollection : ICollectionFixture<AtomicPostgresFixture>
{
    public const string Name = "AtomicPostgres";
}

public sealed class AtomicPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("casinoshiz_atomic_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync().ConfigureAwait(false);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        foreach (var migration in new FrameworkMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new DiceMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new DiceCubeMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new BlackjackMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new BasketballMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new BowlingMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new FootballMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new PickMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new DartsMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new HorseMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new PokerMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new ChallengeMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new RedeemMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new SecretHitlerMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
        foreach (var migration in new PixelBattleMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql).ConfigureAwait(false);
    }

    public async Task DisposeAsync() => await container.DisposeAsync().ConfigureAwait(false);

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync("""
            TRUNCATE TABLE
                game_event_outbox,
                game_schedule_outbox,
                admin_audit,
                game_command_idempotency,
                game_aggregate_states,
                dice_rolls,
                dicecube_bets,
                basketball_bets,
                bowling_bets,
                football_bets,
                pick_chains,
                pick_streaks,
                pick_daily_lottery_tickets,
                pick_daily_lottery,
                pick_lottery_entries,
                pick_lottery,
                darts_rounds,
                horse_bets,
                horse_results,
                poker_seats,
                poker_tables,
                challenge_duels,
                redeem_codes,
                secret_hitler_players,
                secret_hitler_games,
                pixelbattle_tiles,
                economics_ledger,
                telegram_dice_daily_rolls,
                mini_game_sessions,
                users,
                player_protection,
                game_availability_overrides,
                blackjack_hands
            RESTART IDENTITY CASCADE
            """).ConfigureAwait(false);
        await connection.ExecuteAsync(
            "UPDATE runtime_tuning SET payload = '{}'::jsonb, updated_at = now() WHERE id = 1")
            .ConfigureAwait(false);
    }

    public async Task<T> ScalarAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        var result = await connection.ExecuteScalarAsync<T>(sql, parameters).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException("Scalar query returned null.") : result;
    }
}
