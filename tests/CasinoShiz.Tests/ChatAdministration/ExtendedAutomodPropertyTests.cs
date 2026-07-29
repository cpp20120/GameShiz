using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class ExtendedAutomodPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 500)]
    public Property MentionLimitIsNotTriggeredAtOrBelow(NonNegativeInt count)
    {
        var mentions = count.Get % 8;
        var result = new MentionSpamRule(new RuleId("mentions"), new MentionSpamPolicy { Enabled = true, MaximumMentions = 8 })
            .Evaluate(Context(entities: Enumerable.Repeat(new MessageEntity(MessageEntityType.Mention, 0, 1), mentions).ToArray()));
        return (result is null).ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property CommandLimitIsNotTriggeredAtOrBelow(NonNegativeInt count)
    {
        var commands = count.Get % 6;
        var result = new CommandSpamRule(new RuleId("commands"), new CommandSpamPolicy { Enabled = true, MaximumCommands = 6 })
            .Evaluate(Context(
                entities: [new MessageEntity(MessageEntityType.BotCommand, 0, 3)],
                rateLimits: new RateLimitSnapshot { CommandsInWindow = commands }));
        return (result is null).ToProperty();
    }

    [Fact]
    public void ExtendedRulesDetectConfiguredViolations()
    {
        var mention = new MentionSpamRule(new RuleId("mentions"), new MentionSpamPolicy { Enabled = true, MaximumMentions = 1 })
            .Evaluate(Context(entities: [
                new MessageEntity(MessageEntityType.Mention, 0, 1),
                new MessageEntity(MessageEntityType.TextMention, 1, 1),
            ]));
        var forwarded = new ForwardedMessageRule(new RuleId("forwarded"), new ForwardedMessagePolicy { Enabled = true })
            .Evaluate(Context(message: Message() with { IsForwarded = true }));
        var media = new MediaTypeRule(new RuleId("media"), new MediaTypePolicy { BlockedTypes = new HashSet<MessageContentType> { MessageContentType.Video } })
            .Evaluate(Context(message: Message() with { ContentType = MessageContentType.Video }));
        var newMember = new NewMemberRule(new RuleId("new"), new NewMemberPolicy { Enabled = true, Window = TimeSpan.FromMinutes(5) })
            .Evaluate(Context(author: Member() with { FirstSeenAt = Now.AddMinutes(-1) }));
        var command = new CommandSpamRule(new RuleId("commands"), new CommandSpamPolicy { Enabled = true, MaximumCommands = 1 })
            .Evaluate(Context(
                entities: [new MessageEntity(MessageEntityType.BotCommand, 0, 3)],
                rateLimits: new RateLimitSnapshot { CommandsInWindow = 2 }));

        Assert.All(new Violation?[] { mention, forwarded, media, newMember, command }, Assert.NotNull);
    }

    [Fact]
    public void ExtendedRulesRespectDisabledAndNonMatchingMessages()
    {
        Assert.Null(new MentionSpamRule(new RuleId("mentions"), new MentionSpamPolicy()).Evaluate(Context(entities: [
            new MessageEntity(MessageEntityType.Mention, 0, 1),
            new MessageEntity(MessageEntityType.Mention, 1, 1),
            new MessageEntity(MessageEntityType.Url, 2, 1),
        ])));
        Assert.Null(new ForwardedMessageRule(new RuleId("forwarded"), new ForwardedMessagePolicy { Enabled = true }).Evaluate(Context()));
        Assert.Null(new MediaTypeRule(new RuleId("media"), new MediaTypePolicy { BlockedTypes = new HashSet<MessageContentType> { MessageContentType.Video } }).Evaluate(Context()));
        Assert.Null(new NewMemberRule(new RuleId("new"), new NewMemberPolicy { Enabled = true }).Evaluate(Context(author: Member() with { FirstSeenAt = Now.AddMinutes(-20) })));
        Assert.Null(new CommandSpamRule(new RuleId("commands"), new CommandSpamPolicy { Enabled = true, MaximumCommands = 1 }).Evaluate(Context(rateLimits: new RateLimitSnapshot { CommandsInWindow = 2 })));
        Assert.Null(new MentionSpamRule(new RuleId("mentions"), new MentionSpamPolicy { Enabled = true, MaximumMentions = 0 }).Evaluate(Context(entities: [
            new MessageEntity(MessageEntityType.Mention, 0, 1),
        ])));
        Assert.Null(new ForwardedMessageRule(new RuleId("forwarded"), new ForwardedMessagePolicy()).Evaluate(Context(message: Message() with { IsForwarded = true })));
        Assert.Null(new NewMemberRule(new RuleId("new-zero-window"), new NewMemberPolicy { Enabled = true, Window = TimeSpan.Zero }).Evaluate(Context()));
        Assert.Null(new NewMemberRule(new RuleId("new-no-first-seen"), new NewMemberPolicy { Enabled = true }).Evaluate(Context(author: Member() with { FirstSeenAt = default })));
    }

    private static ModerationMessageContext Context(
        NormalizedMessage? message = null,
        MemberState? author = null,
        IReadOnlyList<MessageEntity>? entities = null,
        RateLimitSnapshot? rateLimits = null) => new()
    {
        Chat = new ChatState
        {
            Id = new ChatId(-100),
            Type = ChatType.Supergroup,
            Title = "chat",
            CreatedAt = Now,
            UpdatedAt = Now,
        },
        Author = author ?? Member(),
        Message = message ?? Message() with { Entities = entities ?? [] },
        History = new ModerationHistorySummary(),
        RateLimits = rateLimits ?? new RateLimitSnapshot(),
    };

    private static NormalizedMessage Message() => new()
    {
        ChatId = new ChatId(-100),
        MessageId = 1,
        AuthorId = new UserId(20),
        SentAt = Now,
    };

    private static MemberState Member() => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(20),
        DisplayName = "member",
        FirstSeenAt = Now,
        LastSeenAt = Now,
    };
}
