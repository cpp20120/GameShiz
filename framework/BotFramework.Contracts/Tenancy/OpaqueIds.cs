using System.Text.Json;
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

internal static class OpaqueIdValidation
{
    public static string Validate(string? value, string name)
    {
        if (!TryValidate(value, out var reason))
            throw new ArgumentException(reason, name);

        return value!;
    }

    public static bool TryValidate(string? value, out string reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = "An opaque id is required.";
            return false;
        }

        if (value.Length > 256)
        {
            reason = "An opaque id must contain at most 256 characters.";
            return false;
        }

        if (value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '/' or '\\'))
        {
            reason = "An opaque id must not contain whitespace, path separators, or control characters.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

internal abstract class OpaqueIdJsonConverter<T> : JsonConverter<T>
    where T : struct, IParsable<T>
{
    protected abstract string GetValue(T value);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string for {typeToConvert.Name}.");

        var value = reader.GetString();
        if (value is null || !T.TryParse(value, null, out var result))
            throw new JsonException($"Invalid {typeToConvert.Name}.");

        return result;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(GetValue(value));
}

internal sealed class TenantIdJsonConverter : OpaqueIdJsonConverter<TenantId>
{
    protected override string GetValue(TenantId value) => value.Value;
}

internal sealed class ScopeIdJsonConverter : OpaqueIdJsonConverter<ScopeId>
{
    protected override string GetValue(ScopeId value) => value.Value;
}

internal sealed class PlayerIdJsonConverter : OpaqueIdJsonConverter<PlayerId>
{
    protected override string GetValue(PlayerId value) => value.Value;
}

internal sealed class RequestIdJsonConverter : OpaqueIdJsonConverter<RequestId>
{
    protected override string GetValue(RequestId value) => value.Value;
}
