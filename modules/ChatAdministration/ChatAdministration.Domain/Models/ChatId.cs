namespace ChatAdministration.Domain.Models;

public readonly record struct ChatId(long Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
