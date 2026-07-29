using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class ManualModerationPolicyPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ModerationAction[] Actions =
    [
        ModerationAction.Warn,
        ModerationAction.Unmute,
        ModerationAction.Ban,
        ModerationAction.Unban,
        ModerationAction.Kick,
    ];
    private static readonly ChatMemberRole[] Roles = Enum.GetValues<ChatMemberRole>();

    [Property(MaxTest = 500)]
    public Property AcceptedDecisionMatchesCentralizedAuthorization(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed,
        NonNegativeInt actionSeed)
    {
        var action = Actions[actionSeed.Get % Actions.Length];
        var actor = Member(Roles[actorSeed.Get % Roles.Length], 10);
        var target = Member(Roles[targetSeed.Get % Roles.Length], 20);
        var request = Request(action, action == ModerationAction.Ban ? TimeSpan.FromMinutes(10) : null,
            action == ModerationAction.Ban ? "reason" : null);
        var decision = ManualModerationPolicy.Decide(request, Chat(), actor, target);
        var authorization = AuthorizationPolicy.Authorize(actor, target, PermissionFor(action));

        return (decision.Accepted == authorization.Allowed)
            .ToProperty()
            .Label($"action={action}, actor={actor.Roles.Single()}, target={target.Roles.Single()}, error={decision.ErrorCode}");
    }

    [Property(MaxTest = 500)]
    public Property LowerOrEqualRoleNeverCreatesManualCase(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed,
        NonNegativeInt actionSeed)
    {
        var action = Actions[actionSeed.Get % Actions.Length];
        var actor = Member(Roles[actorSeed.Get % Roles.Length], 10);
        var target = Member(Roles[targetSeed.Get % Roles.Length], 20);
        var decision = ManualModerationPolicy.Decide(
            Request(action, action == ModerationAction.Ban ? TimeSpan.FromMinutes(10) : null),
            Chat(), actor, target);
        var actorRank = AuthorizationPolicy.EffectiveRank(null, actor);
        var targetRank = AuthorizationPolicy.EffectiveRank(null, target);

        return (targetRank >= actorRank ? !decision.Accepted : true)
            .ToProperty()
            .Label($"action={action}, actor={actor.Roles.Single()}, target={target.Roles.Single()}");
    }

    [Property(MaxTest = 500)]
    public Property AcceptedWarnAlwaysCreatesOneActiveWarning(NonNegativeInt seed)
    {
        var reason = seed.Get % 2 == 0 ? null : "flood";
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Warn, null, reason),
            Chat(), Member(ChatMemberRole.Admin, 10), Member(ChatMemberRole.Member, 20));

        return (decision.Accepted
                && decision.Warning is { IsActive: true }
                && decision.Warning.TargetUserId == new UserId(20)
                && decision.Case is { Action: ModerationAction.Warn }
                && decision.Events.OfType<WarningIssued>().Count() == 1
                && decision.EffectPlan.Effects.Count(effect => effect.Importance == EffectImportance.Required) == 0)
            .ToProperty()
            .Label($"reason={reason ?? "none"}");
    }

    [Property(MaxTest = 500)]
    public Property WarningLimitEscalatesOnlyWhenTheNewWarningReachesIt(NonNegativeInt warningSeed)
    {
        var activeWarnings = warningSeed.Get % 6;
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                WarningLimit = 3,
                WarningLimitAction = ModerationAction.Mute,
                WarningLimitMuteDuration = TimeSpan.FromMinutes(2),
            },
        };
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Warn, null, "flood"),
            chat,
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20) with { ActiveWarningCount = activeWarnings });
        var reachesLimit = activeWarnings >= 2;

        return (decision.Accepted
                && (reachesLimit
                    ? decision.Case is { Action: ModerationAction.Mute, ExpiresAt: not null }
                        && decision.EffectPlan.Effects.Any(effect => effect.Effect is RestrictMemberEffect)
                        && decision.Events.OfType<WarningLimitReached>().Any()
                    : decision.Case is { Action: ModerationAction.Warn, ExpiresAt: null }
                        && decision.EffectPlan.Effects.All(effect => effect.Effect is not RestrictMemberEffect)
                        && decision.Events.OfType<WarningLimitReached>().Any() is false))
            .ToProperty()
            .Label($"activeWarnings={activeWarnings}");
    }

    [Fact]
    public void WarningLimitUsesDefaultMuteDurationWhenChatDoesNotConfigureOne()
    {
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Warn, null, "flood"),
            Chat() with
            {
                Settings = new ChatSettings
                {
                    WarningLimit = 1,
                    WarningLimitAction = ModerationAction.Mute,
                    WarningLimitMuteDuration = null,
                },
            },
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20));

        Assert.True(decision.Accepted, decision.ErrorCode);
        Assert.Equal(TimeSpan.FromMinutes(10), decision.Case!.ExpiresAt - Now);
    }

    [Property(MaxTest = 500)]
    public Property TemporaryBanAlwaysHasExpirationDependency(NonNegativeInt durationSeed)
    {
        var duration = TimeSpan.FromSeconds(1 + durationSeed.Get % 86400);
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Ban, duration, "spam"),
            Chat(), Member(ChatMemberRole.Admin, 10), Member(ChatMemberRole.Member, 20));
        var ban = decision.EffectPlan.Effects.Single(effect => effect.Effect is BanMemberEffect);
        var unban = decision.EffectPlan.Effects.Single(effect => effect.Effect is UnbanMemberEffect);

        return (decision.Accepted
                && ban.Importance == EffectImportance.Required
                && unban.Importance == EffectImportance.Required
                && ban.Id is not null
                && unban.DependsOn.Contains(ban.Id.Value)
                && ((BanMemberEffect)ban.Effect).Until == decision.Case!.ExpiresAt)
            .ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property PermanentBanNeverSchedulesUnban(NonNegativeInt seed)
    {
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Ban, null, "fraud"),
            Chat(), Member(ChatMemberRole.Admin, 10), Member(ChatMemberRole.Member, 20));

        return (decision.Accepted
                && decision.Case is { ExpiresAt: null }
                && decision.EffectPlan.Effects.Count(effect => effect.Effect is BanMemberEffect) == 1
                && decision.EffectPlan.Effects.All(effect => effect.Effect is not UnbanMemberEffect))
            .ToProperty();
    }

    [Property(MaxTest = 500)]
    public Property RequiredReasonsRejectBlankValues(NonNegativeInt seed)
    {
        var action = seed.Get % 2 == 0 ? ModerationAction.Warn : ModerationAction.Ban;
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                RequireReasonForWarn = action == ModerationAction.Warn,
                RequireReasonForBan = action == ModerationAction.Ban,
            },
        };
        var decision = ManualModerationPolicy.Decide(
            Request(action, action == ModerationAction.Ban ? TimeSpan.FromMinutes(1) : null, seed.Get % 3 == 0 ? null : "  "),
            chat,
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20));

        return (!decision.Accepted && decision.ErrorCode == "reason_required").ToProperty();
    }

    [Fact]
    public void DisabledModerationCreatesNoManualCase()
    {
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Warn, null, "reason"),
            Chat() with { IsEnabled = false },
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20));

        Assert.False(decision.Accepted);
        Assert.Equal("moderation_disabled", decision.ErrorCode);
        Assert.Empty(decision.Events);
        Assert.Empty(decision.EffectPlan.Effects);
    }

    [Fact]
    public void MuteIsExplicitlyOwnedByMutePolicy()
    {
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Mute, TimeSpan.FromMinutes(1), "reason"),
            Chat(),
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20));

        Assert.False(decision.Accepted);
        Assert.Equal("use_mute_policy", decision.ErrorCode);
    }

    [Fact]
    public void NonPositiveTemporaryBanIsRejected()
    {
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Ban, TimeSpan.Zero, "reason"),
            Chat(),
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20));

        Assert.False(decision.Accepted);
        Assert.Equal("invalid_duration", decision.ErrorCode);
    }

    [Fact]
    public void UnsupportedActionFailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ManualModerationPolicy.Decide(
            Request((ModerationAction)999, null, "reason"),
            Chat(),
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20)));
    }

    [Theory]
    [InlineData(1, "дн.")]
    [InlineData(1, "ч.")]
    public void BanResponseFormatsLongDurations(int amount, string suffix)
    {
        var duration = suffix == "дн." ? TimeSpan.FromDays(amount) : TimeSpan.FromHours(amount);
        var decision = ManualModerationPolicy.Decide(
            Request(ModerationAction.Ban, duration, "reason"),
            Chat(),
            Member(ChatMemberRole.Admin, 10),
            Member(ChatMemberRole.Member, 20));
        var response = Assert.IsType<SendMessageEffect>(decision.EffectPlan.Effects.Single(effect => effect.Effect is SendMessageEffect).Effect);

        Assert.Contains(suffix, response.Text, StringComparison.Ordinal);
    }

    private static ManualModerationRequest Request(ModerationAction action, TimeSpan? duration, string? reason = null) => new(
        new ChatId(-100), new UserId(10), new UserId(20), action, duration, reason, 99,
        "correlation", "causation", Now);

    private static ChatState Chat() => new()
    {
        Id = new ChatId(-100),
        Type = ChatType.Supergroup,
        Title = "test",
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static MemberState Member(ChatMemberRole role, long userId) => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(userId),
        DisplayName = "user",
        Roles = new HashSet<ChatMemberRole> { role },
    };

    private static Permission PermissionFor(ModerationAction action) => action switch
    {
        ModerationAction.Warn => Permission.MembersWarn,
        ModerationAction.Unmute => Permission.MembersUnmute,
        ModerationAction.Ban => Permission.MembersBan,
        ModerationAction.Unban => Permission.MembersUnban,
        ModerationAction.Kick => Permission.MembersKick,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
