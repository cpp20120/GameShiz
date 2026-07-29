namespace ChatAdministration.Domain.Models;

public readonly record struct WarningId(Guid Value)
{
    public static WarningId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
