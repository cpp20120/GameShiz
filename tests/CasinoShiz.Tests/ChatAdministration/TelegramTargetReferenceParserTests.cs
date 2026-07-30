using ChatAdministration.Domain.Models;
using ChatAdministration.Telegram.Presentation;
using Telegram.Bot.Types;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class TelegramTargetReferenceParserTests
{
    [Fact]
    public void ReplyTargetUsesTelegramSenderWhenAvailable()
    {
        var message = CommandMessage(
            "/warn@casino_test_bot",
            new Message
            {
                Id = 41,
                Chat = new Chat { Id = -100 },
                From = new User { Id = 777, IsBot = false, FirstName = "Target", Username = "target" },
            });

        var target = TelegramTargetReferenceParser.FromMessage(message);

        Assert.NotNull(target);
        Assert.Equal(new UserId(777), target.UserId);
        Assert.Equal("target", target.Username);
        Assert.Null(target.SourceMessageId);
    }

    [Fact]
    public void ReplyTargetFallsBackToMessageIndexWhenSenderIsUnavailable()
    {
        var message = CommandMessage(
            "/warn@casino_test_bot",
            new Message
            {
                Id = 41,
                Chat = new Chat { Id = -100 },
            });

        var target = TelegramTargetReferenceParser.FromMessage(message);

        Assert.NotNull(target);
        Assert.Null(target.UserId);
        Assert.Equal(41, target.SourceMessageId);
    }

    [Fact]
    public void MentionOfCommandAuthorDoesNotRequireMemberIndex()
    {
        var message = CommandMessage("/warn@casino_test_bot @CppShizoid");

        var target = TelegramTargetReferenceParser.FromMessage(message);

        Assert.NotNull(target);
        Assert.Equal(new UserId(123), target.UserId);
        Assert.Equal("cppshizoid", target.Username);
    }

    private static Message CommandMessage(string text, Message? reply = null) => new()
    {
        Id = 42,
        Text = text,
        Chat = new Chat { Id = -100 },
        From = new User { Id = 123, IsBot = false, FirstName = "Cppshizoid", Username = "cppshizoid" },
        ReplyToMessage = reply,
    };
}
