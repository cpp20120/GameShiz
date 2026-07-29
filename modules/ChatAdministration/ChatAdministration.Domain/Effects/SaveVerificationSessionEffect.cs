using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record SaveVerificationSessionEffect(
    VerificationSession Session,
    string CorrelationId,
    string CausationId) : ModerationEffect;
