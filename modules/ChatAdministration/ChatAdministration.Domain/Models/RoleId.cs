namespace ChatAdministration.Domain.Models;

public readonly record struct RoleId(string Value)
{
    public override string ToString() => Value;
}
