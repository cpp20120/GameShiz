using BotFramework.Host.Composition.Builder;
using BotFramework.Telegram.Pipeline.Middleware;
using BotFramework.Sdk.UpdateHandling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

[Collection(AtomicPostgresCollection.Name)]
public sealed class UpdateInboxIntegrationTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InboxIsBotScopedAndReplaysOnlyAfterStaleProcessing()
    {
        var middleware = new UpdateDeduplicationMiddleware(
            new ChatAdministrationTestConnectionFactory(database.ConnectionString),
            NullLogger<UpdateDeduplicationMiddleware>.Instance,
            Options.Create(new BotFrameworkOptions { TenantKey = "bot-a" }));
        var calls = 0;
        var update = new Update { Id = 7001 };
        var context = new UpdateContext(null!, update, null!, CancellationToken.None);

        await middleware.InvokeAsync(context, _ =>
        {
            calls++;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, _ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        var otherBot = new UpdateDeduplicationMiddleware(
            new ChatAdministrationTestConnectionFactory(database.ConnectionString),
            NullLogger<UpdateDeduplicationMiddleware>.Instance,
            Options.Create(new BotFrameworkOptions { TenantKey = "bot-b" }));
        await otherBot.InvokeAsync(context, _
            =>
            {
                calls++;
                return Task.CompletedTask;
            });

        Assert.Equal(2, calls);
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM processed_update_inbox WHERE update_id = 7001"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM processed_update_inbox WHERE status = 'completed'"));
    }
}
