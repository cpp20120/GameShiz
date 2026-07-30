using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class TelegramTargetResolverTests
{
    [Fact]
    public async Task ResolvesReplyThroughMessageIndexFallback()
    {
        var expected = new ResolvedTarget(new UserId(777), "target", "Target");
        var store = new RecordingStore { MessageAuthor = expected };
        var resolver = new TelegramTargetResolver(store);

        var resolved = await resolver.ResolveAsync(
            new ChatId(-100),
            TargetReference.ForMessage(41),
            CancellationToken.None);

        Assert.Equal(expected, resolved);
    }
}
