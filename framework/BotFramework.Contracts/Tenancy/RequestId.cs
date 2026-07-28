using System.Text.Json.Serialization;

namespace BotFramework.Contracts.Tenancy;

/// <summary>Opaque request identifier used for tracing and idempotency.</summary>
[JsonConverter(typeof(RequestIdJsonConverter))]
public readonly record struct RequestId : IParsable<RequestId>
{
    public string Value { get; }

    private RequestId(string value) => Value = OpaqueIdValidation.Validate(value, nameof(RequestId));

    public static RequestId Create(string value) => new(value);
    public static RequestId New() => new(Guid.NewGuid().ToString("N"));
    public static RequestId Parse(string s, IFormatProvider? provider) => Create(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out RequestId result)
    {
        if (OpaqueIdValidation.TryValidate(s, out _))
        {
            result = new RequestId(s!);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value;
}
