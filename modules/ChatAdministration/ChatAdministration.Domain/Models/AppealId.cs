namespace ChatAdministration.Domain.Models;

public readonly record struct AppealId(Guid Value)
{
    public static AppealId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
