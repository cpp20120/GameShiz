using BotFramework.Contracts.Messaging;
using BotFramework.Host.Messaging;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Host.Workflows;
using Dapper;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class IntegrationInboxPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS integration_inbox_test_effects (
                message_id TEXT PRIMARY KEY,
                value      TEXT NOT NULL
            );
            TRUNCATE integration_inbox_test_effects;
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DuplicateReturnsStoredResultWithoutRunningCallbackAgain()
    {
        var inbox = CreateInbox();
        var message = CreateMessage("message-1", "tenant-a", "scope-a");
        var callbackCount = 0;

        var first = await inbox.ExecuteOnceAsync(
            message,
            async (context, ct) =>
            {
                callbackCount++;
                await context.Connection.ExecuteAsync(
                    "INSERT INTO integration_inbox_test_effects (message_id, value) VALUES (@messageId, @value)",
                    new { messageId = message.MessageId, value = "accepted" },
                    context.Transaction);
                return "accepted";
            },
            CancellationToken.None);

        var duplicate = await inbox.ExecuteOnceAsync(
            message,
            (_, _) => Task.FromResult("must-not-run"),
            CancellationToken.None);

        Assert.False(first.AlreadyProcessed);
        Assert.Equal("accepted", first.Result);
        Assert.True(duplicate.AlreadyProcessed);
        Assert.Equal("accepted", duplicate.Result);
        Assert.Equal(1, callbackCount);
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM integration_inbox_test_effects"));
    }

    [Fact]
    public async Task SameMessageIdIsIndependentAcrossTenantScopes()
    {
        var inbox = CreateInbox();
        var callbackCount = 0;

        foreach (var tenant in new[] { ("tenant-a", "scope-a"), ("tenant-b", "scope-b") })
        {
            await inbox.ExecuteOnceAsync(
                CreateMessage("same-message", tenant.Item1, tenant.Item2),
                (_, _) =>
                {
                    callbackCount++;
                    return Task.FromResult("done");
                },
                CancellationToken.None);
        }

        Assert.Equal(2, callbackCount);
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM integration_inbox_messages WHERE message_id = 'same-message'"));
    }

    [Fact]
    public async Task FailedCallbackRollsBackInboxAndBusinessWriteForRetry()
    {
        var inbox = CreateInbox();
        var message = CreateMessage("retry-message", "tenant-a", "scope-a");
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => inbox.ExecuteOnceAsync(
            message,
            async (context, ct) =>
            {
                attempts++;
                await context.Connection.ExecuteAsync(
                    "INSERT INTO integration_inbox_test_effects (message_id, value) VALUES (@messageId, @value)",
                    new { messageId = message.MessageId, value = "rolled-back" },
                    context.Transaction);
                throw new InvalidOperationException("transient");
            },
            CancellationToken.None));

        var retry = await inbox.ExecuteOnceAsync(
            message,
            async (context, ct) =>
            {
                attempts++;
                await context.Connection.ExecuteAsync(
                    "INSERT INTO integration_inbox_test_effects (message_id, value) VALUES (@messageId, @value)",
                    new { messageId = message.MessageId, value = "recovered" },
                    context.Transaction);
                return "recovered";
            },
            CancellationToken.None);

        Assert.False(retry.AlreadyProcessed);
        Assert.Equal("recovered", retry.Result);
        Assert.Equal(2, attempts);
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM integration_inbox_test_effects WHERE value = 'recovered'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM integration_inbox_messages WHERE message_id = 'retry-message'"));
    }

    [Fact]
    public async Task OutboxUsesInboxTransactionAndPublishesOnlyAfterSuccessfulCommit()
    {
        var inbox = CreateInbox();
        var outbox = new PostgresIntegrationOutbox(new TestConnectionFactory(database.ConnectionString));
        var message = CreateMessage("outbox-message", "tenant-a", "scope-a");
        var outbound = new IntegrationOutboxMessage(
            "integration-inbox-tests",
            message.MessageId,
            IntegrationMessageKind.Event,
            "casino.events.v1",
            "tenant-a:scope-a:aggregate-1",
            "test.event.v1",
            message.ContractType,
            1,
            "{}",
            "{\"messageId\":\"outbox-message\"}",
            message.OccurredAt,
            message.CorrelationId,
            message.CausationId,
            message.TenantId,
            message.ScopeId,
            message.PlayerId,
            message.Channel);

        await Assert.ThrowsAsync<InvalidOperationException>(() => inbox.ExecuteOnceAsync(
            message,
            async (context, ct) =>
            {
                await outbox.EnqueueAsync(outbound, IntegrationTransactionContext.From(context), ct);
                throw new InvalidOperationException("rollback");
            },
            CancellationToken.None));

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT count(*) FROM integration_outbox_messages WHERE message_id = 'outbox-message'"));

        await inbox.ExecuteOnceAsync(
            message,
            async (context, ct) =>
            {
                await outbox.EnqueueAsync(outbound, IntegrationTransactionContext.From(context), ct);
                return "published";
            },
            CancellationToken.None);

        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM integration_outbox_messages WHERE message_id = 'outbox-message'"));

        var claimed = await outbox.ClaimAsync(
            "integration-inbox-tests",
            10,
            TimeSpan.FromMinutes(1),
            "test-relay",
            CancellationToken.None);
        var delivery = Assert.Single(claimed);
        Assert.Equal("casino.events.v1", delivery.Topic);
        Assert.Equal("tenant-a:scope-a:aggregate-1", delivery.MessageKey);
    }

    [Fact]
    public async Task DurableTimeoutStoreClaimsAndCompletesDueCommand()
    {
        var store = new PostgresDurableWorkflowTimeoutStore(
            new TestConnectionFactory(database.ConnectionString));
        await store.ScheduleAsync(
            new DurableWorkflowTimeoutRequest(
                "timeout-1",
                "workflow-1",
                "command-1",
                "expire",
                DateTimeOffset.UtcNow.AddMinutes(-1),
                new TestTimeoutCommand()),
            null,
            CancellationToken.None);

        var claimed = await store.ClaimDueAsync(
            10,
            TimeSpan.FromMinutes(1),
            "timeout-worker",
            CancellationToken.None);
        var timeout = Assert.Single(claimed);
        Assert.Equal("workflow-1", timeout.WorkflowId);
        Assert.Equal(1, timeout.Attempts);

        await store.MarkDispatchedAsync("timeout-1", "timeout-worker", CancellationToken.None);
        var persisted = Assert.Single(await store.GetByWorkflowAsync("workflow-1", CancellationToken.None));
        Assert.Equal("dispatched", persisted.Status);
    }

    private PostgresIntegrationInbox CreateInbox() =>
        new(
            new TestConnectionFactory(database.ConnectionString),
            new IntegrationInboxOptions("integration-inbox-tests"),
            new IntegrationInboxContextAccessor());

    private static IntegrationInboxMessage CreateMessage(
        string messageId,
        string tenantId,
        string scopeId) =>
        new(
            messageId,
            "test.command.v1",
            "CasinoShiz.Tests:IntegrationInboxTestCommand",
            1,
            "{}",
            DateTimeOffset.UtcNow,
            $"correlation-{messageId}",
            $"causation-{messageId}",
            tenantId,
            scopeId,
            "player-1",
            BotChannel.Rest);

    private sealed class TestConnectionFactory(string connectionString) : INpgsqlConnectionFactory
    {
        public NpgsqlConnection Create() => new(connectionString);

        public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
        {
            var connection = Create();
            await connection.OpenAsync(ct);
            return connection;
        }
    }
}

public sealed record TestTimeoutCommand : IDurableWorkflowCommand;
