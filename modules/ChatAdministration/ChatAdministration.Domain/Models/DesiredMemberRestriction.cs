namespace ChatAdministration.Domain.Models;

public sealed record DesiredMemberRestriction(
    ChatId ChatId,
    UserId UserId,
    RestrictionState State,
    RestrictionState? ObservedState);
