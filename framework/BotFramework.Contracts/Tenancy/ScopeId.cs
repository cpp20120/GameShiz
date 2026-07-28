using System.Text.Json.Serialization;

namespace BotFramework.Contracts.Tenancy;

/// <summary>Stable public identifier for a tenant-owned scope.</summary>
[JsonConverter(typeof(ScopeIdJsonConverter))]
public readonly record struct ScopeId : IParsable<ScopeId>
{
    public string Value { get; }

    private ScopeId(string value) => Value = OpaqueIdValidation.Validate(value, nameof(ScopeId));

    public static ScopeId Create(string value) => new(value);
    public static ScopeId Parse(string s, IFormatProvider? provider) => Create(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out ScopeId result)
    {
        if (OpaqueIdValidation.TryValidate(s, out _))
        {
            result = new ScopeId(s!);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value;
}
