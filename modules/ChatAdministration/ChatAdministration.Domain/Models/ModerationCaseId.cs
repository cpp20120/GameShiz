namespace ChatAdministration.Domain.Models;

public readonly record struct ModerationCaseId(Guid Value)
{
    public static ModerationCaseId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}
