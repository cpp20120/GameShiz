using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed record PersistModerationCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ModerationCaseState Case,
    MemberState Actor,
    MemberState Target,
    IReadOnlyList<IDomainEvent> Events,
    EffectPlan EffectPlan,
    string ResponseText,
    DateTimeOffset CreatedAt,
    WarningState? Warning = null);
