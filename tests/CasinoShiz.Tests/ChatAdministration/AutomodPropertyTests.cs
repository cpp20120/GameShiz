using System.Security.Cryptography;
using System.Text;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class AutomodPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 500)]
    public Property FloodRuleTriggersExactlyAtConfiguredLimit(PositiveInt maximumSeed, NonNegativeInt countSeed)
    {
        var maximum = 1 + maximumSeed.Get % 100;
        var count = countSeed.Get % 150;
        var rule = new FloodRule(new RuleId("flood"), new FloodPolicy { MaximumMessages = maximum });
        var violation = rule.Evaluate(Context(rateLimits: new RateLimitSnapshot { MessagesInWindow = count }));

        return (violation is not null == (count >= maximum))
            .ToProperty()
            .Label($"maximum={maximum}, count={count}");
    }

    [Property(MaxTest = 500)]
    public Property DuplicateRuleOnlyTriggersAfterConfiguredDuplicateCount(NonNegativeInt duplicateSeed)
    {
        var maximum = duplicateSeed.Get % 5;
        var text = "same message";
        var hash = Hash(text);
        var history = Enumerable.Repeat(hash, maximum + 1).ToArray();
        var rule = new DuplicateMessageRule(new RuleId("duplicate"), maximum);
        var violation = rule.Evaluate(Context(message: Message(text), history: new ModerationHistorySummary { RecentMessageHashes = history }));

        return (violation is not null).ToProperty().Label($"maximum={maximum}, duplicates={history.Length}");
    }

    [Property(MaxTest = 500)]
    public Property LinkRuleRespectsTrustedMemberPolicy(NonNegativeInt seed)
    {
        var trusted = seed.Get % 2 == 0;
        var rule = new LinkRule(new RuleId("links"), new LinkPolicy { Mode = LinkPolicyMode.AllowTrusted });
        var author = Member(trusted ? ChatMemberRole.Trusted : ChatMemberRole.Member);
        var violation = rule.Evaluate(Context(author: author, message: Message("https://example.com")));

        return (trusted ? violation is null : violation is not null)
            .ToProperty()
            .Label($"trusted={trusted}");
    }

    [Property(MaxTest = 500)]
    public Property CapsRuleFlagsOnlyMostlyUppercaseText(NonNegativeInt seed)
    {
        var uppercase = seed.Get % 2 == 0;
        var text = uppercase ? "THIS IS LOUD" : "this is calm";
        var rule = new CapsRule(new RuleId("caps"));
        var violation = rule.Evaluate(Context(message: Message(text)));

        return (uppercase ? violation is not null : violation is null).ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property ForbiddenWordsRuleIsCaseInsensitiveByDefault(NonNegativeInt seed)
    {
        var text = seed.Get % 2 == 0 ? "SCAM offer" : "scam offer";
        var rule = new ForbiddenWordsRule(
            new RuleId("words"),
            new ForbiddenWordsPolicy { Words = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scam" } });

        return (rule.Evaluate(Context(message: Message(text))) is not null).ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property AutomodCreatesDeleteCaseForLowScoreViolation(NonNegativeInt seed)
    {
        var rules = new IModerationRule[] { new CapsRule(new RuleId("caps")) };
        var decision = AutomatedModerationPolicy.Decide(Context(message: Message(seed.Get % 2 == 0 ? "THIS IS LOUD" : "LOUD MESSAGE")), rules);

        return (decision.Accepted
                && decision.Case is { Action: ModerationAction.Delete, ActorType: ModerationActorType.AutoMod }
                && decision.EffectPlan.Effects.Any(effect => effect.Effect is DeleteMessageEffect)
                && decision.EffectPlan.Effects.All(effect => effect.Effect is not RestrictMemberEffect))
            .ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property ScoreEscalationIsMonotonic(PositiveInt scoreSeed)
    {
        var score = 1 + scoreSeed.Get % 25;
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                FloodPolicy = new FloodPolicy { DeleteMessages = true },
                ModerationEscalation = new ModerationEscalationPolicy
                {
                    DeleteThreshold = 4,
                    WarningThreshold = 7,
                    MuteThreshold = 10,
                    BanThreshold = 20,
                    MuteDuration = TimeSpan.FromMinutes(10),
                },
            },
        };
        var rule = new ScoreOverrideModerationRule(
            new CapsRule(new RuleId("inner")),
            score);
        var decision = AutomatedModerationPolicy.Decide(
            Context(chat: chat, message: Message("THIS IS LOUD")),
            [rule]);
        var expectedAccepted = score >= 4;
        var expectedAction = score >= 20
            ? ModerationAction.Ban
            : score >= 10
                ? ModerationAction.Mute
                : ModerationAction.Delete;
        var expectedWarning = score >= 7;

        return (decision.Accepted == expectedAccepted
                && (!expectedAccepted || decision.Case!.Action == expectedAction
                    && (decision.Warning is not null) == expectedWarning))
            .ToProperty()
            .Label($"score={score}, action={decision.Case?.Action}, accepted={decision.Accepted}");
    }

    [Fact]
    public void CriticalViolationCreatesDeleteAndTemporaryMute()
    {
        var rules = new IModerationRule[]
        {
            new ForbiddenWordsRule(new RuleId("words"), new ForbiddenWordsPolicy
            {
                Words = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scam" },
            }),
        };
        var decision = AutomatedModerationPolicy.Decide(Context(message: Message("scam")), rules);

        Assert.True(decision.Accepted);
        Assert.Equal(ModerationAction.Mute, decision.Case!.Action);
        Assert.NotNull(decision.Case.ExpiresAt);
        Assert.Contains(decision.EffectPlan.Effects, effect => effect.Effect is DeleteMessageEffect);
        Assert.Contains(decision.EffectPlan.Effects, effect => effect.Effect is RestrictMemberEffect);
    }

    [Fact]
    public void WarningLimitWithDeleteEscalationKeepsDeleteActionAndRecordsLimitEvent()
    {
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                WarningLimit = 1,
                WarningLimitAction = ModerationAction.Delete,
                FloodPolicy = new FloodPolicy { DeleteMessages = true },
                ModerationEscalation = new ModerationEscalationPolicy
                {
                    DeleteThreshold = 4,
                    WarningThreshold = 7,
                    MuteThreshold = 10,
                    BanThreshold = 20,
                },
            },
        };
        var rule = new ScoreOverrideModerationRule(new CapsRule(new RuleId("caps")), 7);

        var decision = AutomatedModerationPolicy.Decide(
            Context(chat: chat, author: Member(ChatMemberRole.Member) with { ActiveWarningCount = 0 }, message: Message("THIS IS LOUD")),
            [rule]);

        Assert.True(decision.Accepted);
        Assert.Equal(ModerationAction.Delete, decision.Case!.Action);
        Assert.Contains(decision.Events, domainEvent => domainEvent is WarningLimitReached);

        var banDecision = AutomatedModerationPolicy.Decide(
            Context(
                chat: chat with { Settings = chat.Settings with { WarningLimitAction = ModerationAction.Ban } },
                author: Member(ChatMemberRole.Member),
                message: Message("THIS IS LOUD")),
            [rule]);

        Assert.Equal(ModerationAction.Ban, banDecision.Case!.Action);
    }

    [Fact]
    public void CriticalViolationWithConfiguredBanDurationSchedulesUnban()
    {
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                FloodPolicy = new FloodPolicy { DeleteMessages = true },
                ModerationEscalation = new ModerationEscalationPolicy
                {
                    BanThreshold = 20,
                    BanDuration = TimeSpan.FromMinutes(5),
                },
            },
        };
        var rule = new ScoreOverrideModerationRule(new CapsRule(new RuleId("caps")), 20);

        var decision = AutomatedModerationPolicy.Decide(
            Context(chat: chat, message: Message("THIS IS LOUD")),
            [rule]);

        Assert.Equal(ModerationAction.Ban, decision.Case!.Action);
        var ban = Assert.Single(decision.EffectPlan.Effects, effect => effect.Effect is BanMemberEffect);
        var unban = Assert.Single(decision.EffectPlan.Effects, effect => effect.Effect is UnbanMemberEffect);
        Assert.Contains(ban.Id!.Value, unban.DependsOn);
    }

    [Fact]
    public void ScoreOverrideDelegatesIdentityToInnerRule()
    {
        var rule = new ScoreOverrideModerationRule(new CapsRule(new RuleId("caps")), 12);

        Assert.Equal(new RuleId("caps"), rule.Id);
        Assert.Null(rule.Evaluate(Context(message: Message("calm text"))));
    }

    [Fact]
    public void ServiceMessagesAndDisabledAutomodAreIgnored()
    {
        var rule = new ForbiddenWordsRule(new RuleId("words"), new ForbiddenWordsPolicy
        {
            Words = new HashSet<string> { "scam" },
        });
        Assert.False(AutomatedModerationPolicy.Decide(Context(chat: Chat() with { IsEnabled = false }, message: Message("scam")), [rule]).Accepted);
        Assert.False(AutomatedModerationPolicy.Decide(Context(message: Message("scam") with { IsServiceMessage = true }), [rule]).Accepted);
        Assert.False(AutomatedModerationPolicy.Decide(Context(message: Message("clean")), [rule]).Accepted);
    }

    [Fact]
    public void RulesCoverAllowAndNoMatchPaths()
    {
        Assert.Null(new FloodRule(new RuleId("flood"), new FloodPolicy { MaximumMessages = 0 })
            .Evaluate(Context()));
        Assert.Null(new DuplicateMessageRule(new RuleId("duplicate"), 1)
            .Evaluate(Context(message: Message("different"), history: new ModerationHistorySummary { RecentMessageHashes = [Hash("same")] })));
        Assert.Null(new DuplicateMessageRule(new RuleId("duplicate"), -1)
            .Evaluate(Context(message: Message(null))));
        Assert.Null(new LinkRule(new RuleId("links"), new LinkPolicy { Mode = LinkPolicyMode.AllowAll })
            .Evaluate(Context(message: Message("https://example.com"))));
        Assert.NotNull(new LinkRule(new RuleId("links"), new LinkPolicy { Mode = LinkPolicyMode.DenyAll })
            .Evaluate(Context(message: new NormalizedMessage
            {
                ChatId = new ChatId(-100),
                MessageId = 99,
                AuthorId = new UserId(20),
                Text = "plain",
                Entities = [new MessageEntity(MessageEntityType.TextLink, 0, 5, "https://example.com")],
                SentAt = Now,
            })));
        Assert.Null(new LinkRule(new RuleId("links"), new LinkPolicy { Mode = LinkPolicyMode.DenyAll })
            .Evaluate(Context(message: Message("plain"))));
        Assert.NotNull(new LinkRule(new RuleId("links"), new LinkPolicy { Mode = LinkPolicyMode.DenyAll })
            .Evaluate(Context(message: new NormalizedMessage
            {
                ChatId = new ChatId(-100),
                MessageId = 99,
                AuthorId = new UserId(20),
                Entities = [new MessageEntity(MessageEntityType.Url, 0, 4)],
                SentAt = Now,
            })));
        Assert.Null(new LinkRule(new RuleId("links"), new LinkPolicy { Mode = LinkPolicyMode.DenyAll })
            .Evaluate(Context(message: Message(null))));
        Assert.Null(new CapsRule(new RuleId("caps"), minimumLetters: 20)
            .Evaluate(Context(message: Message("short"))));
        Assert.Null(new CapsRule(new RuleId("caps"))
            .Evaluate(Context(message: Message(null))));
        Assert.Null(new ForbiddenWordsRule(new RuleId("words"), new ForbiddenWordsPolicy())
            .Evaluate(Context(message: Message("clean"))));
        Assert.Null(new ForbiddenWordsRule(new RuleId("words"), new ForbiddenWordsPolicy { CaseInsensitive = false })
            .Evaluate(Context(message: Message(null))));
    }

    [Fact]
    public void AutomodCanKeepOnlyTheRequiredRestrictionEffect()
    {
        var rule = new ForbiddenWordsRule(new RuleId("words"), new ForbiddenWordsPolicy
        {
            Words = new HashSet<string> { "scam" },
        });
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                FloodPolicy = new FloodPolicy { DeleteMessages = false, MuteDuration = TimeSpan.FromMinutes(2) },
            },
        };
        var decision = AutomatedModerationPolicy.Decide(Context(chat: chat, message: Message("scam")), [rule]);

        Assert.Single(decision.EffectPlan.Effects);
        Assert.IsType<RestrictMemberEffect>(decision.EffectPlan.Effects[0].Effect);
    }

    private static ModerationMessageContext Context(
        ChatState? chat = null,
        MemberState? author = null,
        NormalizedMessage? message = null,
        ModerationHistorySummary? history = null,
        RateLimitSnapshot? rateLimits = null) => new()
    {
        Chat = chat ?? Chat(),
        Author = author ?? Member(ChatMemberRole.Member),
        Message = message ?? Message("hello"),
        History = history ?? new ModerationHistorySummary(),
        RateLimits = rateLimits ?? new RateLimitSnapshot(),
    };

    private static ChatState Chat() => new()
    {
        Id = new ChatId(-100),
        Type = ChatType.Supergroup,
        Title = "chat",
        IsEnabled = true,
        Settings = new ChatSettings { AutoModerationEnabled = true },
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static MemberState Member(ChatMemberRole role) => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(20),
        DisplayName = "member",
        Roles = new HashSet<ChatMemberRole> { role },
    };

    private static NormalizedMessage Message(string? text) => new()
    {
        ChatId = new ChatId(-100),
        MessageId = 99,
        AuthorId = new UserId(20),
        Text = text,
        SentAt = Now,
    };

    private static string Hash(string? text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant())));
}
