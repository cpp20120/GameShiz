namespace ChatAdministration.Domain.Models;

public readonly record struct EffectId(Guid Value)
{
    public static EffectId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}
