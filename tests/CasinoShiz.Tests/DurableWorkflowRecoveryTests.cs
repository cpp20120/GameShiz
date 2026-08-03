using BotFramework.Host.Composition.ServiceDatabases;
using BotFramework.Host.Workflows;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DurableWorkflowRecoveryTests
{
    [Fact]
    public void TimeoutRequestRequiresDurableCommandAndPositiveAttempts()
    {
        var request = new DurableWorkflowTimeoutRequest(
            "timeout-1",
            "workflow-1",
            "command-1",
            "expire",
            DateTimeOffset.UtcNow.AddMinutes(1),
            new TestWorkflowCommand());

        request.Validate();

        Assert.Throws<ArgumentOutOfRangeException>(() => (request with { MaxAttempts = 0 }).Validate());
    }

    [Fact]
    public async Task OwnershipChecksAreOptInForExistingDeployments()
    {
        var validator = new PostgresServiceOwnershipValidator(
            new ThrowingConnectionFactory(),
            Options.Create(new ServiceOwnershipOptions()));

        var report = await validator.ValidateAsync();

        Assert.True(report.IsValid);
        Assert.Empty(report.Violations);
    }

    private sealed record TestWorkflowCommand : IDurableWorkflowCommand;

    private sealed class ThrowingConnectionFactory : BotFramework.Host.Persistence.Connections.INpgsqlConnectionFactory
    {
        public Npgsql.NpgsqlConnection Create() => throw new InvalidOperationException("not expected");

        public Task<Npgsql.NpgsqlConnection> OpenAsync(CancellationToken ct) =>
            throw new InvalidOperationException("not expected");
    }
}
