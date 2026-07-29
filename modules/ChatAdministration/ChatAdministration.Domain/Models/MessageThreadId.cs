namespace ChatAdministration.Domain.Models;

public readonly record struct MessageThreadId(int Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
