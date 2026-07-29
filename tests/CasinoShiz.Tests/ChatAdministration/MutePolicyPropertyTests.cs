using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class MutePolicyPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 500)]
    public Property AcceptedMuteAlwaysCreatesFiniteDesiredRestriction(NonNegativeInt durationSeed)
    {
        var duration = TimeSpan.FromSeconds(1 + durationSeed.Get % 86400);
        var decision = MutePolicy.Decide(
            Request(duration),
            Chat(),
            Member(ChatMemberRole.Moderator, 10),
            Member(ChatMemberRole.Member, 20));

        var desired = decision.DesiredRestriction;
        var caseState = decision.Case;
        var restriction = decision.EffectPlan.Effects.Single(x => x.Effect is RestrictMemberEffect).Effect;

        return (decision.Accepted
                && desired is { CanSendMessages: false, Until: not null }
                && caseState is { Status: ModerationCaseStatus.Requested, ExpiresAt: not null }
                && desired.Until == caseState.ExpiresAt
                && restriction is RestrictMemberEffect applied
                && applied.Until == desired.Until)
            .ToProperty()
            .Label($"duration={duration}");
    }

    [Property(MaxTest = 500)]
    public Property EveryNonPositiveDurationIsRejected(NonNegativeInt durationSeed)
    {
        var duration = TimeSpan.FromSeconds(-(long)(durationSeed.Get % 3601));
        var decision = MutePolicy.Decide(
            Request(duration),
            Chat(),
            Member(ChatMemberRole.Moderator, 10),
            Member(ChatMemberRole.Member, 20));

        return (!decision.Accepted && decision.ErrorCode == "invalid_duration")
            .ToProperty()
            .Label($"duration={duration}");
    }

    [Property(MaxTest = 500)]
    public Property RequiredReasonRejectsBlankReasons(NonNegativeInt reasonSeed)
    {
        var reason = reasonSeed.Get % 2 == 0 ? null : "   ";
        var chat = Chat() with { Settings = new ChatSettings { RequireReasonForMute = true } };
        var decision = MutePolicy.Decide(
            Request(TimeSpan.FromMinutes(1), reason),
            chat,
            Member(ChatMemberRole.Moderator, 10),
            Member(ChatMemberRole.Member, 20));

        return (!decision.Accepted && decision.ErrorCode == "reason_required")
            .ToProperty()
            .Label($"reason={reason ?? "null"}");
    }

    [Property(MaxTest = 500)]
    public Property DisabledManualModerationNeverCreatesEffects(NonNegativeInt disabledSeed)
    {
        var chat = Chat() with
        {
            IsEnabled = disabledSeed.Get % 2 == 0,
            Settings = new ChatSettings { ManualModerationEnabled = disabledSeed.Get % 2 != 0 },
        };
        var decision = MutePolicy.Decide(
            Request(TimeSpan.FromMinutes(1)),
            chat,
            Member(ChatMemberRole.Moderator, 10),
            Member(ChatMemberRole.Member, 20));

        return (!decision.Accepted
                && decision.EffectPlan.Effects.Count == 0
                && decision.Events.Count == 0)
            .ToProperty()
            .Label($"enabled={chat.IsEnabled}, manual={chat.Settings.ManualModerationEnabled}");
    }

    [Property(MaxTest = 500)]
    public Property MuteAcceptanceMatchesCentralizedAuthorization(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed)
    {
        var roles = Enum.GetValues<ChatMemberRole>();
        var actor = Member(roles[actorSeed.Get % roles.Length], 10);
        var target = Member(roles[targetSeed.Get % roles.Length], 20);
        var authorization = AuthorizationPolicy.Authorize(actor, target, Permission.MembersMute);
        var decision = MutePolicy.Decide(Request(TimeSpan.FromMinutes(1)), Chat(), actor, target);

        return (decision.Accepted == authorization.Allowed)
            .ToProperty()
            .Label($"actor={actor.Roles.Single()}, target={target.Roles.Single()}, error={decision.ErrorCode}");
    }

    [Property(MaxTest = 500)]
    public Property ExpirationDependsOnTheRequiredRestriction(NonNegativeInt durationSeed)
    {
        var decision = MutePolicy.Decide(
            Request(TimeSpan.FromSeconds(1 + durationSeed.Get % 86400)),
            Chat(),
            Member(ChatMemberRole.Moderator, 10),
            Member(ChatMemberRole.Member, 20));
        var restriction = decision.EffectPlan.Effects.Single(x => x.Effect is RestrictMemberEffect);
        var expiration = decision.EffectPlan.Effects.Single(x => x.Effect is UnrestrictMemberEffect);

        return (restriction.Importance == EffectImportance.Required
                && expiration.Importance == EffectImportance.Required
                && restriction.Id is not null
                && expiration.DependsOn.Contains(restriction.Id.Value))
            .ToProperty();
    }

    private static MuteRequest Request(TimeSpan duration, string? reason = null) => new(
        new ChatId(-100),
        new UserId(10),
        new UserId(20),
        duration,
        reason,
        "correlation",
        "causation",
        Now);

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
}
