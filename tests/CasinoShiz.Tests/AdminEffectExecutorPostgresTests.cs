using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Admin.Execution;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AdminEffectExecutorPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RuntimePatchAndAudit_CommitTogether()
    {
        var executor = CreateExecutor([new RuntimeConfigurationPatchEffectHandler()]);

        await executor.ExecuteAsync(
            Envelope(),
            new AdminEffectPlan<bool>(true,
            [
                new RuntimeConfigurationPatchEffect("{\"Games\":{\"horse\":{\"HorseCount\":6}}}"),
            ]),
            CancellationToken.None);

        Assert.Equal(6, await database.ScalarAsync<int>(
            "SELECT (payload #>> '{Games,horse,HorseCount}')::int FROM runtime_tuning WHERE id = 1"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM admin_audit WHERE action = 'runtime_tuning.test'"));
    }

    [Fact]
    public async Task LaterEffectFailure_RollsBackPatchAndAudit()
    {
        var executor = CreateExecutor(
        [
            new RuntimeConfigurationPatchEffectHandler(),
            new FailingAdminEffectHandler(),
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            Envelope(),
            new AdminEffectPlan<bool>(true,
            [
                new RuntimeConfigurationPatchEffect("{\"Games\":{\"horse\":{\"HorseCount\":6}}}"),
                new FailingAdminEffect(),
            ]),
            CancellationToken.None));

        Assert.Equal("{}", await database.ScalarAsync<string>(
            "SELECT payload::text FROM runtime_tuning WHERE id = 1"));
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT count(*) FROM admin_audit"));
    }

    private AdminEffectExecutor CreateExecutor(IReadOnlyList<IAdminEffectHandler> handlers)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = database.ConnectionString,
            })
            .Build();
        return new AdminEffectExecutor(new NpgsqlConnectionFactory(configuration), handlers);
    }

    private static AdminExecutionEnvelope Envelope() =>
        new(new AdminActor(42, "test-admin"), "runtime_tuning.test", new { source = "test" });

    private sealed record FailingAdminEffect : IAdminEffect;

    private sealed class FailingAdminEffectHandler : AdminEffectHandler<FailingAdminEffect>
    {
        protected override Task ApplyAsync(
            FailingAdminEffect effect,
            IAdminExecutionContext context,
            CancellationToken ct) =>
            throw new InvalidOperationException("Injected admin effect failure.");
    }
}
