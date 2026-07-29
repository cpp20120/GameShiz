using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class MemberLifecyclePropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 500)]
    public Property WelcomeIsAlwaysBestEffortWhenEnabled(NonNegativeInt seed)
    {
        var decision = MemberLifecyclePolicy.Join(
            Chat() with { Settings = Settings() with { WelcomeEnabled = true, WelcomeTemplate = "{username} {chat} {rules}" } },
            Member(),
            verificationRequired: false,
            Now.AddSeconds(seed.Get % 60));

        var effect = decision.EffectPlan.Effects.Single().Effect;
        return (decision.Accepted
                && decision.Events.Single() is MemberJoined
                && decision.EffectPlan.Effects.Single().Importance == EffectImportance.BestEffort
                && effect is SendMessageEffect message
                && message.Text.Contains("@alice", StringComparison.Ordinal)
                && message.Text.Contains("chat", StringComparison.Ordinal)
                && message.Text.Contains("Правила", StringComparison.Ordinal)).ToProperty();
    }

    [Fact]
    public void CaptchaDelaysWelcomeUntilVerification()
    {
        var decision = MemberLifecyclePolicy.Join(
            Chat() with { Settings = Settings() with { WelcomeEnabled = true } },
            Member(),
            verificationRequired: true,
            Now);

        Assert.Empty(decision.EffectPlan.Effects);
        Assert.IsType<MemberJoined>(Assert.Single(decision.Events));
    }

    [Fact]
    public void DisabledWelcomeDoesNotDependOnVerificationFlag()
    {
        var decision = MemberLifecyclePolicy.Join(
            Chat() with { Settings = Settings() with { WelcomeEnabled = false } },
            Member(),
            verificationRequired: true,
            Now);

        Assert.Empty(decision.EffectPlan.Effects);
        Assert.True(decision.Accepted);
    }

    [Fact]
    public void GoodbyeAndRulesRenderConfiguredContent()
    {
        var chat = Chat() with
        {
            Settings = Settings() with
            {
                GoodbyeEnabled = true,
                GoodbyeTemplate = "bye {username} from {chat}",
                RulesText = "keep calm",
            },
        };
        var goodbye = MemberLifecyclePolicy.Leave(chat, new UserId(20), "Alice", "alice", Now);

        var message = Assert.IsType<SendMessageEffect>(Assert.Single(goodbye.EffectPlan.Effects).Effect);
        Assert.Equal("bye @alice from chat", message.Text);
        Assert.Equal("keep calm", MemberLifecyclePolicy.RenderRules(chat));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void WelcomeTemplateFallsBackToDisplayNameWhenBlank(string template)
    {
        var effect = MemberLifecyclePolicy.CreateWelcomeEffect(
            Chat() with { Settings = Settings() with { WelcomeTemplate = template } },
            Member());

        Assert.Equal("Alice", effect.Text);
    }

    [Fact]
    public void NullWelcomeTemplateAlsoFallsBackSafely()
    {
        var effect = MemberLifecyclePolicy.CreateWelcomeEffect(
            Chat() with { Settings = Settings() with { WelcomeTemplate = null! } },
            Member());

        Assert.Equal("Alice", effect.Text);
    }

    [Fact]
    public void MissingUsernameUsesDisplayNameForUsernamePlaceholder()
    {
        var effect = MemberLifecyclePolicy.CreateWelcomeEffect(
            Chat() with { Settings = Settings() with { WelcomeTemplate = "{username}" } },
            Member() with { Username = null });

        Assert.Equal("Alice", effect.Text);
    }

    [Fact]
    public void InvalidLifecycleInputsAreRejected()
    {
        Assert.Equal("chat_disabled", MemberLifecyclePolicy.Join(Chat() with { IsEnabled = false }, Member(), false, Now).ErrorCode);
        Assert.Equal("member_chat_mismatch", MemberLifecyclePolicy.Join(Chat(), Member() with { ChatId = new ChatId(-200) }, false, Now).ErrorCode);
        Assert.Equal("chat_disabled", MemberLifecyclePolicy.Leave(Chat() with { IsEnabled = false }, new UserId(20), "Alice", null, Now).ErrorCode);
        Assert.Equal("invalid_user", MemberLifecyclePolicy.Leave(Chat(), new UserId(0), "Alice", null, Now).ErrorCode);
        Assert.Empty(MemberLifecyclePolicy.Leave(Chat(), new UserId(20), "Alice", null, Now).EffectPlan.Effects);
        Assert.Equal("Правила чата пока не настроены.", MemberLifecyclePolicy.RenderRules(Chat() with { Settings = Settings() with { RulesText = " " } }));
    }

    private static ChatState Chat() => new()
    {
        Id = new ChatId(-100),
        Type = ChatType.Supergroup,
        Title = "chat",
        IsEnabled = true,
        Settings = Settings(),
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static ChatSettings Settings() => new() { RulesText = "Правила" };

    private static MemberState Member() => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(20),
        Username = "alice",
        DisplayName = "Alice",
        FirstSeenAt = Now,
        LastSeenAt = Now,
    };
}
