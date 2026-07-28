using System.Text.Json.Serialization;

namespace BotFramework.Contracts.Tenancy;

/// <summary>Stable public identifier for a tenant.</summary>
[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId : IParsable<TenantId>
{
    public string Value { get; }

    private TenantId(string value) => Value = OpaqueIdValidation.Validate(value, nameof(TenantId));

    public static TenantId Create(string value) => new(value);
    public static TenantId Parse(string s, IFormatProvider? provider) => Create(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out TenantId result)
    {
        if (OpaqueIdValidation.TryValidate(s, out _))
        {
            result = new TenantId(s!);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value;
}
