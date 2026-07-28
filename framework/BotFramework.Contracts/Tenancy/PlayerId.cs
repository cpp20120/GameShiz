using System.Text.Json.Serialization;

namespace BotFramework.Contracts.Tenancy;

/// <summary>Stable public identifier for a player inside a tenant.</summary>
[JsonConverter(typeof(PlayerIdJsonConverter))]
public readonly record struct PlayerId : IParsable<PlayerId>
{
    public string Value { get; }

    private PlayerId(string value) => Value = OpaqueIdValidation.Validate(value, nameof(PlayerId));

    public static PlayerId Create(string value) => new(value);
    public static PlayerId Parse(string s, IFormatProvider? provider) => Create(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out PlayerId result)
    {
        if (OpaqueIdValidation.TryValidate(s, out _))
        {
            result = new PlayerId(s!);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value;
}
