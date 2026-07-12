using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicTurnStatePostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FrameworkStore_InsertsLoadsAndUpdatesVersionedJsonState()
    {
        var descriptor = new Descriptor();
        var store = new PostgresJsonGameStateStore<Command, State, Result>(descriptor);
        var command = new Command(42, "create");

        await InTransaction(async (context, ct) =>
        {
            var initial = await store.LoadAsync(command, context, ct);
            Assert.Equal(0, initial.Revision);
            await store.SaveVersionedAsync(
                command,
                initial with { Revision = 1, Moves = 1 },
                0,
                context,
                ct);
        });

        await InTransaction(async (context, ct) =>
        {
            var loaded = await store.LoadAsync(command, context, ct);
            Assert.Equal(new State(1, 1), loaded);
            await store.SaveVersionedAsync(
                command,
                loaded with { Revision = 2, Moves = 2 },
                1,
                context,
                ct);
        });

        Assert.Equal(2, await database.ScalarAsync<long>(
            "SELECT version FROM game_aggregate_states WHERE game_id = 'turn-store-test'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT CAST(state ->> 'moves' AS integer) FROM game_aggregate_states WHERE game_id = 'turn-store-test'"));
    }

    [Fact]
    public async Task FrameworkStore_RejectsUnexpectedRevision()
    {
        var descriptor = new Descriptor();
        var store = new PostgresJsonGameStateStore<Command, State, Result>(descriptor);
        var command = new Command(42, "first");
        await InTransaction((context, ct) =>
            store.SaveVersionedAsync(command, new State(1, 1), 0, context, ct));

        await Assert.ThrowsAsync<GameStateConcurrencyException>(() =>
            InTransaction((context, ct) =>
                store.SaveVersionedAsync(command, new State(3, 3), 2, context, ct)));

        Assert.Equal(1, await database.ScalarAsync<long>(
            "SELECT version FROM game_aggregate_states WHERE game_id = 'turn-store-test'"));
    }

    private async Task InTransaction(Func<IGameExecutionContext, CancellationToken, Task> action)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        await using var session = await new PostgresGameExecutionSessionFactory(connections)
            .BeginAsync(CancellationToken.None);
        try
        {
            await action(new GameExecutionContext(session), CancellationToken.None);
            await session.CommitAsync(CancellationToken.None);
        }
        catch
        {
            await session.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public sealed record Command(long PlayerId, string CommandId);

    public sealed record State(long Revision, int Moves) : IVersionedGameState;

    public sealed record Result(bool Ok);

    public sealed class Descriptor : GameExecutionDescriptor<Command, State, Result>
    {
        public override string GameId => "turn-store-test";
        public override string CommandId(Command command) => command.CommandId;
        public override string AggregateId(Command command) => command.PlayerId.ToString(CultureInfo.InvariantCulture);
        public override long ChatId(Command command) => 1;
        public override string DisplayName(Command command) => "player";
        public override WalletIdentity Wallet(Command command) => new(command.PlayerId, 1);
        public override State CreateInitialState(Command command) => new(0, 0);
    }

    private sealed class TestConnectionFactory(string connectionString) : INpgsqlConnectionFactory
    {
        public NpgsqlConnection Create() => new(connectionString);

        public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
        {
            var connection = Create();
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return connection;
        }
    }
}
