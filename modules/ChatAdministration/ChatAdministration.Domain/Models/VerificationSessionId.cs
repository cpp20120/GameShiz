namespace ChatAdministration.Domain.Models;

public readonly record struct VerificationSessionId(Guid Value)
{
    public static VerificationSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
