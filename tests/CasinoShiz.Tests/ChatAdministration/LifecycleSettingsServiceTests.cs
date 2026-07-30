using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class LifecycleSettingsServiceTests
{
    [Fact]
    public async Task SuccessfulUpdateRepliesToTheSourceCommand()
    {
        var store = new RecordingStore { ActorRole = ChatMemberRole.Owner };
        var service = new LifecycleSettingsService(store);
        var command = new UpdateLifecycleSettingsCommand(
            "command-1",
            "idempotency-1",
            "correlation-1",
            new ChatId(-100),
            new UserId(123),
            "Hi",
            null,
            null,
            null,
            null,
            42,
            DateTimeOffset.UtcNow,
            ChatMemberRole.Owner,
            "Cppshizoid");

        var response = await service.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal("✅ Lifecycle-настройки обновлены.", response);
        Assert.Equal(42, store.LastResponseReplyToMessageId);
        Assert.Equal("Hi", store.UpdatedSettings?.WelcomeTemplate);
    }
}
