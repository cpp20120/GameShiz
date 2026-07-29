using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class VerificationPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 500)]
    public Property EveryValidChallengeCreatesPendingSession(NonNegativeInt seed)
    {
        var options = seed.Get % 2 == 0 ? new[] { "yes", "no" } : new[] { "✅", "❌", "🤖" };
        var decision = VerificationPolicy.Start(Chat(), Member(), options, options[0], Now);

        return (decision.Accepted
                && decision.Session is { Status: VerificationStatus.Pending }
                && decision.Session.Options.SequenceEqual(options)
                && decision.EffectPlan.Effects.Count(effect => effect.Importance == EffectImportance.Required) == 2
                && decision.EffectPlan.Effects[1].DependsOn.Count == 1)
            .ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property CorrectAnswerAlwaysPasses(NonNegativeInt seed)
    {
        var session = Session(maximumAttempts: 1);
        var decision = VerificationPolicy.Submit(session, Chat(), session.UserId, "callback", "yes", 123, Now.AddMinutes(1));

        return (decision.Accepted
                && decision.Session is { Status: VerificationStatus.Passed }
                && decision.Events.Single() is VerificationPassed
                && decision.EffectPlan.Effects.Any(effect => effect.Effect is UnrestrictMemberEffect))
            .ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property WrongAnswerDoesNotPunishBeforeAttemptLimit(PositiveInt attemptsSeed)
    {
        var attempts = attemptsSeed.Get % 3;
        var session = Session(maximumAttempts: 3) with { Attempts = attempts };
        var decision = VerificationPolicy.Submit(session, Chat(), session.UserId, "callback", "no", 123, Now.AddMinutes(1));

        return (decision.Accepted
                && decision.Session is not null
                && decision.Session.Status == (attempts + 1 >= session.MaximumAttempts ? VerificationStatus.Failed : VerificationStatus.Pending)
                && decision.Events.Single() is VerificationFailed failed
                && failed.IsFinal == (attempts + 1 >= session.MaximumAttempts))
            .ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property ChallengeCleanupNeverDeletesAnotherMessage(NonNegativeInt seed)
    {
        var mode = seed.Get % 4;
        var storedMessageId = mode switch
        {
            0 => (int?)null,
            1 => 123,
            2 => 456,
            _ => 123,
        };
        var callbackMessageId = mode == 3 ? 0 : 123;
        var expectedDeletion = callbackMessageId > 0
            && (storedMessageId is null || storedMessageId == callbackMessageId);
        var session = Session(1) with { ChallengeMessageId = storedMessageId };

        var passed = VerificationPolicy.Submit(
            session,
            Chat(),
            session.UserId,
            "callback",
            "yes",
            callbackMessageId,
            Now.AddMinutes(1));
        var failed = VerificationPolicy.Submit(
            session,
            Chat(),
            session.UserId,
            "callback",
            "no",
            callbackMessageId,
            Now.AddMinutes(1));
        var expired = VerificationPolicy.Expire(
            session with { ExpiresAt = Now.AddMinutes(-1) },
            Chat(),
            callbackMessageId,
            Now);

        return (ContainsDelete(passed) == expectedDeletion
                && ContainsDelete(failed) == expectedDeletion
                && ContainsDelete(expired) == expectedDeletion)
            .ToProperty()
            .Label($"mode={mode}, stored={storedMessageId}, callback={callbackMessageId}");
    }

    [Fact]
    public void DisabledAndMalformedChallengesAreRejected()
    {
        Assert.Equal("captcha_disabled", VerificationPolicy.Start(Chat(false), Member(), ["yes"], "yes", Now).ErrorCode);
        Assert.Equal("captcha_disabled", VerificationPolicy.Start(Chat() with { IsEnabled = false }, Member(), ["yes"], "yes", Now).ErrorCode);
        Assert.Equal("invalid_timeout", VerificationPolicy.Start(Chat(true) with { Settings = Settings(true) with { CaptchaPolicy = new CaptchaPolicy { Enabled = true, Timeout = TimeSpan.Zero } } }, Member(), ["yes"], "yes", Now).ErrorCode);
        Assert.Equal("invalid_attempt_limit", VerificationPolicy.Start(Chat(true) with { Settings = Settings(true) with { CaptchaPolicy = new CaptchaPolicy { Enabled = true, Timeout = TimeSpan.FromMinutes(1), MaximumAttempts = 0 } } }, Member(), ["yes"], "yes", Now).ErrorCode);
        Assert.Equal("invalid_challenge", VerificationPolicy.Start(Chat(), Member(), [], "yes", Now).ErrorCode);
        Assert.Equal("invalid_challenge", VerificationPolicy.Start(Chat(), Member(), ["no"], "yes", Now).ErrorCode);
    }

    [Fact]
    public void CallbackRejectsCompletedForeignAndInvalidAnswers()
    {
        var session = Session();
        Assert.Equal("verification_completed", VerificationPolicy.Submit(session with { Status = VerificationStatus.Passed }, Chat(), session.UserId, "callback", "yes", 123, Now).ErrorCode);
        Assert.Equal("verification_actor_mismatch", VerificationPolicy.Submit(session, Chat(), new UserId(99), "callback", "yes", 123, Now).ErrorCode);
        Assert.Equal("verification_actor_mismatch", VerificationPolicy.Submit(session, Chat() with { Id = new ChatId(-200) }, session.UserId, "callback", "yes", 123, Now).ErrorCode);
        Assert.Equal("invalid_answer", VerificationPolicy.Submit(session, Chat(), session.UserId, "callback", "maybe", 123, Now).ErrorCode);
    }

    [Fact]
    public void ExpiredCallbackUsesConfiguredPunishment()
    {
        var session = Session() with { ExpiresAt = Now.AddMinutes(-1) };
        var decision = VerificationPolicy.Submit(session, Chat(), session.UserId, "callback", "no", 123, Now);

        Assert.True(decision.Accepted);
        Assert.Equal(VerificationStatus.Expired, decision.Session!.Status);
        Assert.Contains(decision.EffectPlan.Effects, effect => effect.Effect is KickMemberEffect);
    }

    [Fact]
    public void ExpirationRequiresExpiredPendingSession()
    {
        var session = Session();
        Assert.Equal("verification_not_expired", VerificationPolicy.Expire(session, Chat(), 123, Now).ErrorCode);
        Assert.Equal("verification_completed", VerificationPolicy.Expire(session with { Status = VerificationStatus.Failed }, Chat(), 123, Now.AddHours(1)).ErrorCode);
    }

    [Fact]
    public void FinalFailureAndExpirationCanBanAndKeepChallengeMessage()
    {
        var chat = Chat() with
        {
            Settings = Settings(true) with
            {
                CaptchaPolicy = new CaptchaPolicy
                {
                    Enabled = true,
                    Timeout = TimeSpan.FromMinutes(1),
                    MaximumAttempts = 1,
                    FailureAction = CaptchaFailureAction.Ban,
                    DeleteChallengeAfterCompletion = false,
                },
            },
        };
        var failed = VerificationPolicy.Submit(Session(1), chat, new UserId(20), "callback", "no", 123, Now);
        var expired = VerificationPolicy.Expire(Session() with { ExpiresAt = Now.AddMinutes(-1) }, chat, 123, Now);

        Assert.Contains(failed.EffectPlan.Effects, effect => effect.Effect is BanMemberEffect);
        Assert.DoesNotContain(failed.EffectPlan.Effects, effect => effect.Effect is DeleteMessageEffect);
        Assert.Contains(expired.EffectPlan.Effects, effect => effect.Effect is BanMemberEffect);
        Assert.DoesNotContain(expired.EffectPlan.Effects, effect => effect.Effect is DeleteMessageEffect);
    }

    [Fact]
    public void PassingWithoutChallengeMessageDoesNotCreateDeleteEffect()
    {
        var decision = VerificationPolicy.Submit(Session(), Chat(), new UserId(20), "callback", "yes", 0, Now);
        var disabledDelete = VerificationPolicy.Submit(
            Session(),
            Chat() with
            {
                Settings = Settings(true) with
                {
                    CaptchaPolicy = Settings(true).CaptchaPolicy with { DeleteChallengeAfterCompletion = false },
                },
            },
            new UserId(20),
            "callback",
            "yes",
            123,
            Now);

        Assert.DoesNotContain(decision.EffectPlan.Effects, effect => effect.Effect is DeleteMessageEffect);
        Assert.DoesNotContain(disabledDelete.EffectPlan.Effects, effect => effect.Effect is DeleteMessageEffect);
    }

    [Theory]
    [InlineData(null, 123, true)]
    [InlineData(123, 123, true)]
    [InlineData(456, 123, false)]
    [InlineData(123, 0, false)]
    public void ChallengeCleanupCoversStoredMessageIdentity(int? storedMessageId, int callbackMessageId, bool expectedDeletion)
    {
        var session = Session(1) with { ChallengeMessageId = storedMessageId };

        var passed = VerificationPolicy.Submit(session, Chat(), session.UserId, "callback", "yes", callbackMessageId, Now.AddMinutes(1));
        var failed = VerificationPolicy.Submit(session, Chat(), session.UserId, "callback", "no", callbackMessageId, Now.AddMinutes(1));
        var expired = VerificationPolicy.Expire(session with { ExpiresAt = Now.AddMinutes(-1) }, Chat(), callbackMessageId, Now);

        Assert.Equal(expectedDeletion, ContainsDelete(passed));
        Assert.Equal(expectedDeletion, ContainsDelete(failed));
        Assert.Equal(expectedDeletion, ContainsDelete(expired));
    }

    private static ChatState Chat(bool enabled = true) => new()
    {
        Id = new ChatId(-100),
        Type = ChatType.Supergroup,
        Title = "chat",
        IsEnabled = true,
        Settings = Settings(enabled),
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static ChatSettings Settings(bool enabled) => new()
    {
        CaptchaPolicy = new CaptchaPolicy
        {
            Enabled = enabled,
            Timeout = TimeSpan.FromMinutes(5),
            MaximumAttempts = 3,
            FailureAction = CaptchaFailureAction.Kick,
            DeleteChallengeAfterCompletion = true,
        },
    };

    private static MemberState Member() => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(20),
        DisplayName = "member",
    };

    private static VerificationSession Session(int maximumAttempts = 3) => new()
    {
        Id = VerificationSessionId.New(),
        ChatId = new ChatId(-100),
        UserId = new UserId(20),
        CorrectAnswer = "yes",
        Options = ["yes", "no"],
        MaximumAttempts = maximumAttempts,
        CreatedAt = Now,
        ExpiresAt = Now.AddMinutes(5),
    };

    private static bool ContainsDelete(VerificationDecision decision) =>
        decision.EffectPlan.Effects.Any(effect => effect.Effect is DeleteMessageEffect);
}
