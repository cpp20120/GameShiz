namespace ChatAdministration.Domain.Models;

public readonly record struct UserId(long Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
